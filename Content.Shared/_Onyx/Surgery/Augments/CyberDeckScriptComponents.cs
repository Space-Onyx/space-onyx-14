using Content.Shared.Access;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent]
public sealed partial class CyberDeckScriptImplantFailureComponent : Component
{
    [DataField] public float Range = 7f;
    [DataField] public float MinDisableDuration = 5f;
    [DataField] public float MaxDisableDuration = 6f;
    [DataField] public bool AffectSelf;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberDeckScriptRemoteDeactivationComponent : Component
{
    [DataField, AutoNetworkedField] public float Range = 10f;
    [DataField, AutoNetworkedField] public float OperationDelay = 2f;
    [DataField, AutoNetworkedField] public float TargetSearchRadius = 1.2f;
    [DataField] public float MinCameraDisableDuration = 6f;
    [DataField] public float MaxCameraDisableDuration = 8f;
    [DataField, AutoNetworkedField] public List<ProtoId<AccessLevelPrototype>> Access = new();
    [DataField, AutoNetworkedField] public bool Inverted;
    [DataField, AutoNetworkedField] public Color OverlayFillColor = new(24, 132, 255, 26);
    [DataField, AutoNetworkedField] public Color OverlayOuterOutlineColor = new(0, 0, 0, 230);
    [DataField, AutoNetworkedField] public Color OverlayInnerOutlineColor = new(24, 132, 255, 245);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberDeckScriptOpticsOverloadComponent : Component
{
    [DataField, AutoNetworkedField] public float Range = 7f;
    [DataField, AutoNetworkedField] public float RangeWithoutOptics = 6f;
    [DataField, AutoNetworkedField] public float OperationDelay = 1f;
    [DataField, AutoNetworkedField] public float TargetSearchRadius = 1.2f;
    [DataField] public float MinDisableDuration = 5f;
    [DataField] public float MaxDisableDuration = 6f;
    [DataField, AutoNetworkedField] public Color OverlayFillColor = new(255, 52, 134, 52);
    [DataField, AutoNetworkedField] public Color OverlayOuterOutlineColor = new(0, 0, 0, 230);
    [DataField, AutoNetworkedField] public Color OverlayInnerOutlineColor = new(255, 52, 134, 245);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberDeckScriptMotorImpairmentComponent : Component
{
    [DataField, AutoNetworkedField] public float Range = 7f;
    [DataField, AutoNetworkedField] public float OperationDelay = 1f;
    [DataField, AutoNetworkedField] public float TargetSearchRadius = 1.2f;
    [DataField] public float MinDisableDuration = 4f;
    [DataField] public float MaxDisableDuration = 5f;
    [DataField, AutoNetworkedField] public Color OverlayFillColor = new(255, 124, 34, 58);
    [DataField, AutoNetworkedField] public Color OverlayOuterOutlineColor = new(0, 0, 0, 230);
    [DataField, AutoNetworkedField] public Color OverlayInnerOutlineColor = new(255, 160, 52, 245);
}

[Serializable, NetSerializable]
public sealed partial class CyberDeckScriptDoAfterEvent : DoAfterEvent
{
    [DataField] public NetEntity TargetEntity;
    [DataField] public NetEntity Body;
    [DataField] public NetEntity CyberDeck;

    public override DoAfterEvent Clone() => new CyberDeckScriptDoAfterEvent
    {
        TargetEntity = TargetEntity,
        Body = Body,
        CyberDeck = CyberDeck,
    };
}
