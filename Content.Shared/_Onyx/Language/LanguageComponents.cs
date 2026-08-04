using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Language;

[RegisterComponent, NetworkedComponent]
public sealed partial class LanguageSpeakerComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField]
    public ProtoId<LanguagePrototype> CurrentLanguage = "Universal";

    [DataField]
    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = new();

    [DataField]
    public HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();

    public bool UnderstandsAllLanguages;
}

[Serializable, NetSerializable]
public sealed class LanguageSpeakerComponentState(
    ProtoId<LanguagePrototype> currentLanguage,
    HashSet<ProtoId<LanguagePrototype>> spokenLanguages,
    HashSet<ProtoId<LanguagePrototype>> understoodLanguages,
    bool understandsAllLanguages) : ComponentState
{
    public ProtoId<LanguagePrototype> CurrentLanguage = currentLanguage;
    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = spokenLanguages;
    public HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages = understoodLanguages;
    public bool UnderstandsAllLanguages = understandsAllLanguages;
}

[RegisterComponent]
public sealed partial class LanguageKnowledgeComponent : Component
{
    [DataField("speaks", required: true)]
    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = new();

    [DataField("understands", required: true)]
    public HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UniversalLanguageSpeakerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = new() { "Psychomantic" };

    [DataField]
    public bool UnderstandsAllLanguages = true;
}

public abstract partial class BaseTranslatorComponent : Component
{
    [DataField("spoken")]
    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = new();

    [DataField("understood")]
    public HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();

    [DataField("requires")]
    public HashSet<ProtoId<LanguagePrototype>> RequiredLanguages = new();

    [DataField]
    public bool Enabled = true;

    [DataField("requiresAll")]
    public bool RequiresAllLanguages;
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
