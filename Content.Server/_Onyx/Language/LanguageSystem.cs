using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._Onyx.Language;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Language;

public sealed partial class LanguageSystem : EntitySystem
{
    private static readonly ProtoId<LanguagePrototype> Universal = "Universal";

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LanguageSpeakerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LanguageSpeakerComponent, ComponentGetState>(OnGetState);
        SubscribeNetworkEvent<SetLanguageMessage>(OnSetLanguage);
        SubscribeLocalEvent<HandheldTranslatorComponent, ActivateInWorldEvent>(OnTranslatorToggle);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotInsertedIntoContainerMessage>(OnTranslatorInserted);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotRemovedFromContainerMessage>(OnTranslatorRemoved);
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotInsertedIntoContainerMessage>(OnImplantInserted);
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotRemovedFromContainerMessage>(OnImplantRemoved);
    }

    private void OnGetState(Entity<LanguageSpeakerComponent> ent, ref ComponentGetState args)
    {
        args.State = new LanguageSpeakerComponentState(
            ent.Comp.CurrentLanguage,
            ent.Comp.SpokenLanguages,
            ent.Comp.UnderstoodLanguages);
    }

    private void OnSetLanguage(SetLanguageMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } speaker)
            SetLanguage(speaker, message.Language);
    }

    private void OnMapInit(Entity<LanguageSpeakerComponent> ent, ref MapInitEvent args)
    {
        ResetToIntrinsicLanguages(ent.Owner, ent.Comp, out _);

        Timer.Spawn(0, () =>
        {
            if (Exists(ent.Owner))
                UpdateLanguages(ent.Owner);
        });
    }

    private void OnTranslatorToggle(Entity<HandheldTranslatorComponent> ent, ref ActivateInWorldEvent args)
    {
        ent.Comp.Enabled = !ent.Comp.Enabled;
        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
        {
            var holder = container.Owner;
            UpdateLanguages(holder);
            if (ent.Comp.Enabled)
            {
                var newLanguage = ent.Comp.SpokenLanguages.FirstOrDefault(language =>
                    !TryComp<LanguageKnowledgeComponent>(holder, out var knowledge) || !knowledge.SpokenLanguages.Contains(language));
                if (newLanguage != default)
                    SetLanguage(holder, newLanguage);
            }
        }
        args.Handled = true;
    }

    private void OnTranslatorInserted(Entity<HandheldTranslatorComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        UpdateLanguages(args.Container.Owner);
    }

    private void OnTranslatorRemoved(Entity<HandheldTranslatorComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        var holder = args.Container.Owner;
        Timer.Spawn(0, () =>
        {
            if (Exists(holder))
                UpdateLanguages(holder);
        });
    }

    private void OnImplantInserted(Entity<TranslatorImplantComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        UpdateLanguages(args.Container.Owner);
    }

    private void OnImplantRemoved(Entity<TranslatorImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        var holder = args.Container.Owner;
        Timer.Spawn(0, () =>
        {
            if (Exists(holder))
                UpdateLanguages(holder);
        });
    }

    public void UpdateLanguages(EntityUid speaker)
    {
        if (!TryComp<LanguageSpeakerComponent>(speaker, out var languageSpeaker))
            return;

        ResetToIntrinsicLanguages(speaker, languageSpeaker, out var knowledge);

        if (TryComp<ContainerManagerComponent>(speaker, out var manager) && manager.Containers != null)
        {
            foreach (var container in manager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (TryComp<HandheldTranslatorComponent>(contained, out var translator))
                        AddTranslatorLanguages(translator, knowledge, languageSpeaker);
                }
            }
        }

        if (TryComp<ImplantedComponent>(speaker, out var implanted))
        {
            foreach (var implant in implanted.ImplantContainer.ContainedEntities)
            {
                if (TryComp<TranslatorImplantComponent>(implant, out var translator))
                    AddTranslatorLanguages(translator, knowledge, languageSpeaker);
            }
        }

        EnsureValidCurrentLanguage(languageSpeaker);
        Dirty(speaker, languageSpeaker);
    }

    private void ResetToIntrinsicLanguages(
        EntityUid speaker,
        LanguageSpeakerComponent languageSpeaker,
        out LanguageKnowledgeComponent? knowledge)
    {
        languageSpeaker.SpokenLanguages.Clear();
        languageSpeaker.UnderstoodLanguages.Clear();
        if (TryComp(speaker, out knowledge))
        {
            languageSpeaker.SpokenLanguages.UnionWith(knowledge.SpokenLanguages);
            languageSpeaker.UnderstoodLanguages.UnionWith(knowledge.UnderstoodLanguages);
        }

        EnsureValidCurrentLanguage(languageSpeaker);
    }

    private static void EnsureValidCurrentLanguage(LanguageSpeakerComponent languageSpeaker)
    {
        if (!languageSpeaker.SpokenLanguages.Contains(languageSpeaker.CurrentLanguage))
            languageSpeaker.CurrentLanguage = languageSpeaker.SpokenLanguages.FirstOrDefault(Universal);
    }

    private static void AddTranslatorLanguages(
        BaseTranslatorComponent translator,
        LanguageKnowledgeComponent? knowledge,
        LanguageSpeakerComponent speaker)
    {
        if (!translator.Enabled || knowledge == null ||
            translator.RequiredLanguages.Count > 0 && !translator.RequiredLanguages.Any(knowledge.UnderstoodLanguages.Contains))
            return;

        speaker.SpokenLanguages.UnionWith(translator.SpokenLanguages);
        speaker.UnderstoodLanguages.UnionWith(translator.UnderstoodLanguages);
    }

    public LanguagePrototype GetCurrentLanguage(EntityUid speaker)
    {
        if (TryComp<LanguageSpeakerComponent>(speaker, out var component) &&
            _prototypes.TryIndex(component.CurrentLanguage, out LanguagePrototype? language))
            return language;

        return _prototypes.Index(Universal);
    }

    public bool CanUnderstand(EntityUid listener, ProtoId<LanguagePrototype> language)
    {
        return HasComp<UniversalLanguageSpeakerComponent>(listener) ||
               language == Universal ||
               TryComp<LanguageSpeakerComponent>(listener, out var component) && component.UnderstoodLanguages.Contains(language);
    }

    public string Obfuscate(string message, LanguagePrototype language)
    {
        return language.Obfuscation.Obfuscate(message, _ticker.RoundId);
    }

    public bool SetLanguage(EntityUid speaker, ProtoId<LanguagePrototype> language)
    {
        if (!TryComp<LanguageSpeakerComponent>(speaker, out var component) || !component.SpokenLanguages.Contains(language))
            return false;

        component.CurrentLanguage = language;
        Dirty(speaker, component);
        return true;
    }
}
