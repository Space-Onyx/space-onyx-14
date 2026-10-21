// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._Onyx.Language;
using Content.Shared.Abilities.Mime;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Language;

/// <summary>Composes source knowledge, resolves equipment, publishes one authoritative snapshot.</summary>
public sealed partial class LanguageSystem : SharedLanguageSystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ServerGameTicker _ticker = default!;

    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LanguageSpeakerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LanguageSpeakerComponent, ComponentStartup>(OnSpeakerStartup);
        SubscribeLocalEvent<LanguageSpeakerComponent, LanguageKnowledgeChangedEvent>(OnKnowledgeChanged);
        SubscribeLocalEvent<LanguageKnowledgeComponent, CollectLanguageKnowledgeEvent>(OnCollectKnowledge);
        SubscribeLocalEvent<LanguageKnowledgeComponent, ComponentStartup>(OnKnowledgeStartup);
        SubscribeLocalEvent<LanguageKnowledgeComponent, ComponentShutdown>(OnKnowledgeShutdown);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, CollectLanguageKnowledgeEvent>(OnCollectUniversal);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, ComponentStartup>(OnUniversalStartup);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, ComponentShutdown>(OnUniversalShutdown);
        SubscribeNetworkEvent<SetLanguageMessage>(OnSetLanguage);
        SubscribeLocalEvent<MimePowersComponent, CollectLanguageKnowledgeEvent>(OnCollectMime);
        SubscribeLocalEvent<MimePowersComponent, ComponentStartup>(OnMimeStartup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_pending.Count == 0)
            return;
        var pending = _pending.ToArray();
        _pending.Clear();
        foreach (var entity in pending)
            UpdateLanguages(entity);
    }

    public void QueueUpdate(EntityUid entity) => _pending.Add(entity);

    private void OnMapInit(Entity<LanguageSpeakerComponent> ent, ref MapInitEvent args) => UpdateLanguages(ent);
    private void OnSpeakerStartup(Entity<LanguageSpeakerComponent> ent, ref ComponentStartup args) => QueueUpdate(ent);
    private void OnKnowledgeChanged(Entity<LanguageSpeakerComponent> ent, ref LanguageKnowledgeChangedEvent args) => QueueUpdate(ent);
    private void OnKnowledgeStartup(Entity<LanguageKnowledgeComponent> ent, ref ComponentStartup args) => QueueUpdate(ent);
    private void OnKnowledgeShutdown(Entity<LanguageKnowledgeComponent> ent, ref ComponentShutdown args) => QueueUpdate(ent);
    private void OnUniversalStartup(Entity<UniversalLanguageSpeakerComponent> ent, ref ComponentStartup args) => QueueUpdate(ent);
    private void OnUniversalShutdown(Entity<UniversalLanguageSpeakerComponent> ent, ref ComponentShutdown args) => QueueUpdate(ent);
    private void OnMimeStartup(Entity<MimePowersComponent> ent, ref ComponentStartup args) => QueueUpdate(ent);

    private void OnCollectMime(Entity<MimePowersComponent> ent, ref CollectLanguageKnowledgeEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping)
            args.Grant("Sign", LanguageAbility.Speak | LanguageAbility.Understand);
    }

    private void OnCollectKnowledge(Entity<LanguageKnowledgeComponent> ent, ref CollectLanguageKnowledgeEvent args)
    {
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;
        foreach (var (language, ability) in ent.Comp.Languages)
            args.Grant(language, ability);
    }

    private void OnCollectUniversal(Entity<UniversalLanguageSpeakerComponent> ent, ref CollectLanguageKnowledgeEvent args)
    {
        if (!ent.Comp.Enabled || ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;
        foreach (var (language, ability) in ent.Comp.Languages)
            args.Grant(language, ability);
        args.UniversalAbilities |= ent.Comp.Abilities;
    }

    private void OnSetLanguage(SetLanguageMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } entity)
            SetLanguage(entity, message.Language, message.Writing);
    }

    /// <summary>Changes intrinsic knowledge; equipment and temporary sources remain independent.</summary>
    public bool SetKnowledge(EntityUid entity, ProtoId<LanguagePrototype> language, LanguageAbility abilities)
    {
        if (!_prototypes.TryIndex(language, out var prototype) || TerminatingOrDeleted(entity) || (abilities & ~LanguageAbility.All) != 0)
            return false;
        if (!prototype.CanWrite)
            abilities &= ~(LanguageAbility.Read | LanguageAbility.Write);
        var knowledge = EnsureComp<LanguageKnowledgeComponent>(entity);
        EnsureComp<LanguageSpeakerComponent>(entity);
        if (abilities == LanguageAbility.None)
            knowledge.Languages.Remove(language);
        else
            knowledge.Languages[language] = abilities;
        UpdateLanguages(entity);
        return true;
    }

    public void UpdateLanguages(EntityUid entity)
    {
        if (TerminatingOrDeleted(entity) || !TryComp<LanguageSpeakerComponent>(entity, out var speaker))
            return;

        var collected = new CollectLanguageKnowledgeEvent();
        RaiseLocalEvent(entity, collected);
        if (collected.UniversalAbilities != LanguageAbility.None)
        {
            foreach (var language in _prototypes.EnumeratePrototypes<LanguagePrototype>())
                collected.Grant(language.ID, collected.UniversalAbilities);
        }
        var translated = new TranslateLanguageKnowledgeEvent(collected);
        RaiseLocalEvent(entity, translated);

        foreach (var (id, ability) in collected.Languages.ToArray())
        {
            if (!_prototypes.TryIndex(id, out var language))
            {
                collected.Languages.Remove(id);
                continue;
            }
            var supported = language.CanWrite ? ability : ability & ~(LanguageAbility.Read | LanguageAbility.Write);
            if (supported == LanguageAbility.None)
                collected.Languages.Remove(id);
            else
                collected.Languages[id] = supported;
        }

        var current = speaker.CurrentLanguage;
        if (current is not { } selected || !LanguageKnowledge.Has(collected.Languages, selected, LanguageAbility.Speak))
        {
            current = collected.Languages
                .Where(entry => (entry.Value & LanguageAbility.Speak) != 0)
                .OrderByDescending(entry => _prototypes.Index(entry.Key).SelectionPriority)
                .ThenBy(entry => entry.Key.Id, StringComparer.Ordinal)
                .Select(entry => (ProtoId<LanguagePrototype>?) entry.Key).FirstOrDefault();
        }

        var written = speaker.WrittenLanguage;
        if (written is not { } writing || !LanguageKnowledge.Has(collected.Languages, writing, LanguageAbility.Write))
        {
            written = current is { } spoken && LanguageKnowledge.Has(collected.Languages, spoken, LanguageAbility.Write)
                ? spoken
                : collected.Languages.Where(entry => (entry.Value & LanguageAbility.Write) != 0)
                    .OrderBy(entry => entry.Key.Id, StringComparer.Ordinal)
                    .Select(entry => (ProtoId<LanguagePrototype>?) entry.Key).FirstOrDefault();
        }

        if (current == speaker.CurrentLanguage && written == speaker.WrittenLanguage && collected.UniversalAbilities == speaker.UniversalAbilities &&
            collected.Languages.Count == speaker.Languages.Count &&
            collected.Languages.All(entry => speaker.Languages.GetValueOrDefault(entry.Key) == entry.Value))
            return;

        speaker.Languages = collected.Languages;
        speaker.UniversalAbilities = collected.UniversalAbilities;
        speaker.CurrentLanguage = current;
        speaker.WrittenLanguage = written;
        Publish(entity, speaker);
    }

    public bool SetLanguage(EntityUid entity, ProtoId<LanguagePrototype> language, bool writing = false)
    {
        if (!(writing ? CanWrite(entity, language) : CanSpeak(entity, language)) ||
            !TryComp<LanguageSpeakerComponent>(entity, out var speaker))
            return false;
        if (writing)
        {
            if (speaker.WrittenLanguage == language)
                return true;
            speaker.WrittenLanguage = language;
            Publish(entity, speaker);
            return true;
        }
        if (speaker.CurrentLanguage == language)
            return true;
        speaker.CurrentLanguage = language;
        if (CanWrite(entity, language))
            speaker.WrittenLanguage = language;
        Publish(entity, speaker);
        return true;
    }

    private void Publish(EntityUid entity, LanguageSpeakerComponent speaker)
    {
        speaker.Revision++;
        Dirty(entity, speaker);
        var changed = new LanguageStateChangedEvent();
        RaiseLocalEvent(entity, ref changed);
    }

    public string Obfuscate(string text, LanguagePrototype language) =>
        LanguageText.Obfuscate(text, language, _ticker.RoundId, LanguageText.InlineActionLength);

    public string Obfuscate(string text, LanguagePrototype language, Func<string, int, int> protectedLength) =>
        LanguageText.Obfuscate(text, language, _ticker.RoundId, protectedLength);
}
