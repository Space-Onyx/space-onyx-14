// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Language;

/// <summary>Owner-only effective knowledge. Modified only by the authoritative language system.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LanguageSpeakerComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public ProtoId<LanguagePrototype>? CurrentLanguage;

    [DataField, AutoNetworkedField]
    public ProtoId<LanguagePrototype>? WrittenLanguage;

    [AutoNetworkedField]
    public Dictionary<ProtoId<LanguagePrototype>, LanguageAbility> Languages = new();

    [AutoNetworkedField]
    public LanguageAbility UniversalAbilities;

    [AutoNetworkedField]
    public uint Revision;
}

/// <summary>Intrinsic abilities, independent of equipment and temporary sources.</summary>
[RegisterComponent]
public sealed partial class LanguageKnowledgeComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<LanguagePrototype>, LanguageAbility> Languages = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UniversalLanguageSpeakerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public Dictionary<ProtoId<LanguagePrototype>, LanguageAbility> Languages = new()
    {
        ["Psychomantic"] = LanguageAbility.Speak,
    };

    [DataField]
    public LanguageAbility Abilities = LanguageAbility.Understand | LanguageAbility.Read;
}

public abstract partial class BaseTranslatorComponent : Component
{
    [DataField]
    public bool TranslateWriting;

    [DataField]
    public Dictionary<ProtoId<LanguagePrototype>, LanguageAbility> Languages = new();

    [DataField]
    public HashSet<ProtoId<LanguagePrototype>> Requires = new();

    [DataField]
    public bool Enabled = true;

    [DataField]
    public bool RequiresAll;
}

[RegisterComponent]
public sealed partial class HandheldTranslatorComponent : BaseTranslatorComponent
{
    [DataField]
    public bool SetLanguageOnInteract = true;

    [DataField]
    public bool ShowInfoOnExamine = true;
}

[RegisterComponent]
public sealed partial class TranslatorImplantComponent : BaseTranslatorComponent;
