// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Language;

[Serializable, NetSerializable]
public sealed class SetLanguageMessage(ProtoId<LanguagePrototype> language, bool writing = false) : EntityEventArgs
{
    public readonly ProtoId<LanguagePrototype> Language = language;
    public readonly bool Writing = writing;
}

/// <summary>Sources add abilities; they must never remove another source's contribution.</summary>
public sealed class CollectLanguageKnowledgeEvent : EntityEventArgs
{
    public readonly Dictionary<ProtoId<LanguagePrototype>, LanguageAbility> Languages = new();
    public LanguageAbility UniversalAbilities;

    public void Grant(ProtoId<LanguagePrototype> language, LanguageAbility ability)
    {
        LanguageKnowledge.Grant(Languages, language, ability);
    }
}

/// <summary>Equipment resolves only after intrinsic and temporary sources have been collected.</summary>
public sealed class TranslateLanguageKnowledgeEvent(CollectLanguageKnowledgeEvent knowledge) : EntityEventArgs
{
    public readonly CollectLanguageKnowledgeEvent Knowledge = knowledge;
}

[ByRefEvent]
public readonly record struct LanguageStateChangedEvent;

public sealed class LanguageKnowledgeChangedEvent : EntityEventArgs;
