using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._Onyx.Language;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Language;

public sealed partial class LanguageSystem : EntitySystem
{
    private static readonly ProtoId<LanguagePrototype> Universal = "Universal";
    private static readonly ProtoId<LanguagePrototype> Psychomantic = "Psychomantic";

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TranslatorSystem _translator = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LanguageSpeakerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LanguageSpeakerComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, ComponentStartup>(OnUniversalStartup);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, ComponentRemove>(OnUniversalRemoved);
        SubscribeNetworkEvent<SetLanguageMessage>(OnSetLanguage);
        SubscribeLocalEvent<HandheldTranslatorComponent, ActivateInWorldEvent>(OnTranslatorToggle);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotInsertedIntoContainerMessage>(OnTranslatorInserted);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotRemovedFromContainerMessage>(OnTranslatorRemoved);
        SubscribeLocalEvent<HandheldTranslatorComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<HandheldTranslatorComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<HandheldTranslatorComponent, ItemToggledEvent>(OnItemToggled);
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotInsertedIntoContainerMessage>(OnImplantInserted);
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotRemovedFromContainerMessage>(OnImplantRemoved);
    }

    private void OnUniversalStartup(Entity<UniversalLanguageSpeakerComponent> ent, ref ComponentStartup args)
    {
        UpdateLanguages(ent);
    }

    private void OnUniversalRemoved(Entity<UniversalLanguageSpeakerComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<LanguageSpeakerComponent>(ent, out var speaker))
        {
            speaker.SpokenLanguages.Remove(Psychomantic);
            EnsureValidCurrentLanguage(speaker);
            Dirty(ent.Owner, speaker);
        }
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
        var hasPower = _powerCell.HasDrawCharge(ent.Owner, user: args.User);
        var enabled = !ent.Comp.Enabled && hasPower;
        ent.Comp.Enabled = enabled;
        _powerCell.SetDrawEnabled(ent.Owner, enabled);
        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
        {
            var holder = container.Owner;
            UpdateLanguages(holder);
            if (enabled && ent.Comp.SetLanguageOnInteract)
            {
                var newLanguage = ent.Comp.SpokenLanguages.FirstOrDefault(language =>
                    !TryComp<LanguageKnowledgeComponent>(holder, out var knowledge) || !knowledge.SpokenLanguages.Contains(language));
                if (newLanguage != default)
                    SetLanguage(holder, newLanguage);
            }
        }

        _translator.UpdateAppearance(ent);
        if (hasPower)
        {
            var message = Loc.GetString(enabled ? "translator-component-turnon" : "translator-component-shutoff",
                ("translator", ent.Owner));
            _popup.PopupEntity(message, ent.Owner, args.User);
        }
        args.Handled = true;
    }

    private void OnPowerCellSlotEmpty(Entity<HandheldTranslatorComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        SetTranslatorEnabled(ent, false);
    }

    private void OnPowerCellChanged(Entity<HandheldTranslatorComponent> ent, ref PowerCellChangedEvent args)
    {
        SetTranslatorEnabled(ent, _powerCell.HasActivatableCharge(ent.Owner));
    }

    private void OnItemToggled(Entity<HandheldTranslatorComponent> ent, ref ItemToggledEvent args)
    {
        SetTranslatorEnabled(ent, args.Activated && _powerCell.HasActivatableCharge(ent.Owner));
    }

    private void SetTranslatorEnabled(Entity<HandheldTranslatorComponent> ent, bool enabled)
    {
        ent.Comp.Enabled = enabled;
        _powerCell.SetDrawEnabled(ent.Owner, enabled);
        _translator.UpdateAppearance(ent);

        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
            UpdateLanguages(container.Owner);
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
        TryComp(speaker, out knowledge);
        if (knowledge != null)
        {
            languageSpeaker.SpokenLanguages.UnionWith(knowledge.SpokenLanguages);
            languageSpeaker.UnderstoodLanguages.UnionWith(knowledge.UnderstoodLanguages);
        }

        if (TryComp<UniversalLanguageSpeakerComponent>(speaker, out var universal) && universal.Enabled)
            languageSpeaker.SpokenLanguages.Add(Psychomantic);

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
            !CheckLanguagesMatch(translator.RequiredLanguages, knowledge.UnderstoodLanguages, translator.RequiresAllLanguages))
            return;

        speaker.SpokenLanguages.UnionWith(translator.SpokenLanguages);
        speaker.UnderstoodLanguages.UnionWith(translator.UnderstoodLanguages);
    }

    private static bool CheckLanguagesMatch<T>(ICollection<T> required, ICollection<T> provided, bool requireAll)
    {
        if (required.Count == 0)
            return true;

        return requireAll ? required.All(provided.Contains) : required.Any(provided.Contains);
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
        return (TryComp<UniversalLanguageSpeakerComponent>(listener, out var universal) && universal.Enabled) ||
               language == Psychomantic ||
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
