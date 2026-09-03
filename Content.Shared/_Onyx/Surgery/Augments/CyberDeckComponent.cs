using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberDeckComponent : Component
{
    [DataField]
    public float BaseMaxRam = 8f;

    [DataField, AutoNetworkedField]
    public float MaxRam = 8f;

    [DataField, AutoNetworkedField]
    public float CurrentRam = 8f;

    [DataField]
    public float RamRegenTime = 3f;

    [DataField(required: true)]
    public EntProtoId OpenAction;

    [AutoNetworkedField]
    public EntityUid? OpenActionEntity;

    public float RegenAccumulator;
    public EntityUid? GrantedBody;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CyberDeckRamModuleComponent : Component
{
    [DataField]
    public float RamIncrease = 4f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberDeckScriptComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public float RamCost = 4f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CyberDeckScriptActivatableUIComponent : Component
{
    [DataField(required: true, customTypeSerializer: typeof(EnumSerializer))]
    public Enum? Key;
}

public sealed partial class CyberDeckOpenActionEvent : InstantActionEvent;
public sealed partial class CyberDeckScriptActionEvent : InstantActionEvent;
public sealed partial class CyberDeckScriptTargetActionEvent : WorldTargetActionEvent;

[ByRefEvent]
public record struct CyberDeckScriptExecutedEvent(
    EntityUid Body,
    EntityUid CyberDeck,
    EntityUid Performer,
    EntityUid? TargetEntity = null,
    EntityCoordinates? TargetCoordinates = null,
    bool Handled = false);
