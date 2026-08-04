using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._Onyx.Language;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Language;

public sealed partial class LanguageSystem : EntitySystem
{
    private static readonly ProtoId<LanguagePrototype> Universal = "Universal";

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private GameTicker _ticker = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LanguageSpeakerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LanguageSpeakerComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<LanguageKnowledgeComponent, CollectLanguageKnowledgeEvent>(OnCollectKnowledge);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, CollectLanguageKnowledgeEvent>(OnCollectUniversalKnowledge);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, ComponentStartup>(OnUniversalStartup);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, ComponentRemove>(OnUniversalRemoved);
        SubscribeNetworkEvent<SetLanguageMessage>(OnSetLanguage);
    }

    private void OnUniversalStartup(Entity<UniversalLanguageSpeakerComponent> ent, ref ComponentStartup args)
    {
        UpdateLanguages(ent);
    }

    private void OnUniversalRemoved(Entity<UniversalLanguageSpeakerComponent> ent, ref ComponentRemove args)
    {
        Timer.Spawn(0, () =>
        {
            if (Exists(ent.Owner))
                UpdateLanguages(ent.Owner);
        });
    }

    private void OnGetState(Entity<LanguageSpeakerComponent> ent, ref ComponentGetState args)
    {
        args.State = new LanguageSpeakerComponentState(
            ent.Comp.CurrentLanguage,
            ent.Comp.SpokenLanguages,
            ent.Comp.UnderstoodLanguages,
            ent.Comp.UnderstandsAllLanguages);
    }

    private void OnSetLanguage(SetLanguageMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } speaker)
            SetLanguage(speaker, message.Language);
    }

    private void OnMapInit(Entity<LanguageSpeakerComponent> ent, ref MapInitEvent args)
    {
        UpdateLanguages(ent.Owner);

        Timer.Spawn(0, () =>
        {
            if (Exists(ent.Owner))
                UpdateLanguages(ent.Owner);
        });
    }

    private void OnCollectKnowledge(Entity<LanguageKnowledgeComponent> ent, ref CollectLanguageKnowledgeEvent args)
    {
        args.SpokenLanguages.UnionWith(ent.Comp.SpokenLanguages);
        args.UnderstoodLanguages.UnionWith(ent.Comp.UnderstoodLanguages);
    }

    private void OnCollectUniversalKnowledge(
        Entity<UniversalLanguageSpeakerComponent> ent,
        ref CollectLanguageKnowledgeEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        args.SpokenLanguages.UnionWith(ent.Comp.SpokenLanguages);
        args.UnderstandsAllLanguages |= ent.Comp.UnderstandsAllLanguages;
    }

    public void UpdateLanguages(EntityUid speaker)
    {
        if (!TryComp<LanguageSpeakerComponent>(speaker, out var languageSpeaker))
            return;

        var knowledge = new CollectLanguageKnowledgeEvent();
        RaiseLocalEvent(speaker, knowledge);
        languageSpeaker.SpokenLanguages.Clear();
        languageSpeaker.SpokenLanguages.UnionWith(knowledge.SpokenLanguages);
        languageSpeaker.UnderstoodLanguages.Clear();
        languageSpeaker.UnderstoodLanguages.UnionWith(knowledge.UnderstoodLanguages);
        languageSpeaker.UnderstandsAllLanguages = knowledge.UnderstandsAllLanguages;

        EnsureValidCurrentLanguage(languageSpeaker);
        Dirty(speaker, languageSpeaker);
    }

    private static void EnsureValidCurrentLanguage(LanguageSpeakerComponent languageSpeaker)
    {
        if (!languageSpeaker.SpokenLanguages.Contains(languageSpeaker.CurrentLanguage))
            languageSpeaker.CurrentLanguage = languageSpeaker.SpokenLanguages.FirstOrDefault(Universal);
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
        return _prototypes.TryIndex(language, out var prototype) && prototype.AlwaysUnderstood ||
               TryComp<LanguageSpeakerComponent>(listener, out var component) &&
               (component.UnderstandsAllLanguages || component.UnderstoodLanguages.Contains(language));
    }

    public bool CanSpeak(EntityUid speaker, ProtoId<LanguagePrototype> language)
    {
        return _prototypes.HasIndex(language) &&
               TryComp<LanguageSpeakerComponent>(speaker, out var component) &&
               component.SpokenLanguages.Contains(language);
    }

    public string Obfuscate(string message, LanguagePrototype language)
    {
        return language.Obfuscation.Obfuscate(message, _ticker.RoundId);
    }

    public bool SetLanguage(EntityUid speaker, ProtoId<LanguagePrototype> language)
    {
        if (!CanSpeak(speaker, language) || !TryComp<LanguageSpeakerComponent>(speaker, out var component))
            return false;

        component.CurrentLanguage = language;
        Dirty(speaker, component);
        return true;
    }
}
