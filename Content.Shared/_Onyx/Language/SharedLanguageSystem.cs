// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Language;

/// <summary>Modality-aware language API shared by every consumer.</summary>
public abstract partial class SharedLanguageSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _languagePrototypes = default!;

    public bool HasAbility(EntityUid entity, ProtoId<LanguagePrototype> language, LanguageAbility ability)
    {
        if (!_languagePrototypes.TryIndex(language, out var prototype) ||
            (ability & (LanguageAbility.Read | LanguageAbility.Write)) != 0 && !prototype.CanWrite)
            return false;

        if (prototype.AlwaysUnderstood && (ability & ~(LanguageAbility.Understand | LanguageAbility.Read)) == 0)
            return true;

        if (!TryComp<LanguageSpeakerComponent>(entity, out var speaker))
            return false;
        var available = speaker.UniversalAbilities | speaker.Languages.GetValueOrDefault(language);
        return (available & ability) == ability;
    }

    public bool CanSpeak(EntityUid speaker, ProtoId<LanguagePrototype> language) =>
        HasAbility(speaker, language, LanguageAbility.Speak);

    public bool CanUnderstand(EntityUid listener, ProtoId<LanguagePrototype> language) =>
        HasAbility(listener, language, LanguageAbility.Understand);

    public bool CanRead(EntityUid reader, ProtoId<LanguagePrototype> language) =>
        HasAbility(reader, language, LanguageAbility.Read);

    public bool CanWrite(EntityUid writer, ProtoId<LanguagePrototype> language) =>
        HasAbility(writer, language, LanguageAbility.Write);

    /// <summary>Resolves an already-authorized override used by server relays and admin speech.</summary>
    public bool TryResolveLanguage(EntityUid entity, ProtoId<LanguagePrototype>? authorizedOverride, out LanguagePrototype language)
    {
        if (authorizedOverride is { } id && _languagePrototypes.TryIndex(id, out var prototype))
        {
            language = prototype;
            return true;
        }
        return TryGetCurrentLanguage(entity, out language);
    }

    public bool TryGetWrittenLanguage(EntityUid entity, out LanguagePrototype language)
    {
        if (TryComp<LanguageSpeakerComponent>(entity, out var speaker) && speaker.WrittenLanguage is { } id &&
            CanWrite(entity, id) && _languagePrototypes.TryIndex(id, out var prototype))
        {
            language = prototype;
            return true;
        }
        language = default!;
        return false;
    }

    /// <summary>Non-speaker machines use Universal; speakers without a language cannot speak.</summary>
    public bool TryGetCurrentLanguage(EntityUid entity, out LanguagePrototype language)
    {
        var id = TryComp<LanguageSpeakerComponent>(entity, out var speaker)
            ? speaker.CurrentLanguage
            : new ProtoId<LanguagePrototype>("Universal");
        if (id is { } selected && (speaker == null || CanSpeak(entity, selected)) &&
            _languagePrototypes.TryIndex(selected, out var prototype))
        {
            language = prototype;
            return true;
        }
        language = default!;
        return false;
    }
}
