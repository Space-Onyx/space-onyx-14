using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Wounds;

[RegisterComponent, NetworkedComponent]
public sealed partial class WoundHostComponent : Component
{
    [DataField]
    public Dictionary<BodyPartType, float> TargetWeights = new()
    {
        [BodyPartType.Torso] = 4f,
        [BodyPartType.Chest] = 2.5f,
        [BodyPartType.Groin] = 1.5f,
        [BodyPartType.Head] = 1f,
        [BodyPartType.Arm] = 2f,
        [BodyPartType.Hand] = 1f,
        [BodyPartType.Leg] = 2f,
        [BodyPartType.Foot] = 1f,
        [BodyPartType.Tail] = 1f,
        [BodyPartType.Other] = 1f,
    };

    [DataField]
    public HashSet<ProtoId<DamageTypePrototype>> LocalizedDamageTypes =
    [
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Cold",
        "Shock",
        "Caustic",
    ];
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PartDamageVisualsComponent : Component
{
    [AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, DamageSpecifier> Damage = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PainComponent : Component
{
    [AutoNetworkedField]
    public FixedPoint2 Value;

    [AutoNetworkedField]
    public FixedPoint2 Suppression;

    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> DamageMultipliers = new()
    {
        ["Blunt"] = 0.87f,
        ["Slash"] = 0.67f,
        ["Piercing"] = 0.67f,
        ["Heat"] = 0.8f,
        ["Cold"] = 0.75f,
        ["Shock"] = 0.7f,
        ["Cellular"] = 0.32f,
        ["Caustic"] = 0.12f,
        ["Radiation"] = 0.12f,
        ["Poison"] = 0.7f,
    };

    [DataField]
    public FixedPoint2 RecoveryPerSecond = FixedPoint2.New(1f / 9f);

    [DataField]
    public FixedPoint2 SoftPainCap = 135;

    [ViewVariables]
    public Dictionary<string, PainSuppressionModifier> SuppressionModifiers = new();
}

public sealed record PainSuppressionModifier(FixedPoint2 Amount, FixedPoint2 DecayPerSecond, float RecoveryMultiplier);

[RegisterComponent]
public sealed partial class PainShockTargetComponent : Component;

[RegisterComponent]
public sealed partial class PainShockComponent : Component
{
    [DataField]
    public bool WasSleeping;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundableComponent : Component
{
    public const string ContainerId = "wounds";

    [DataField, AutoNetworkedField]
    public ProtoId<WoundableProfilePrototype> Profile = "OrganicWoundableProfile";

    [ViewVariables]
    public Container WoundsContainer = default!;
}

[RegisterComponent]
public sealed partial class ScarlessComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid HoldingPart;

    [DataField(required: true), AutoNetworkedField]
    public ProtoId<WoundPrototype> Prototype;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Severity;

    [DataField, AutoNetworkedField]
    public FixedPoint2 PeakSeverity;

    [DataField, AutoNetworkedField]
    public WoundState State = WoundState.Open;

    [ViewVariables]
    public bool ScarCreatedForCurrentClosure;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundBleedingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BaseRate;

    [DataField, AutoNetworkedField]
    public float CurrentRate;

    [DataField, AutoNetworkedField]
    public FixedPoint2 BleedingSeverity;

    [DataField, AutoNetworkedField]
    public BleedingTreatment Treatment;

    [DataField, AutoNetworkedField]
    public float NaturalClotting;

    [ViewVariables]
    public TimeSpan? AutomaticClottingStartedAt;

    [ViewVariables]
    public TimeSpan? AutomaticClottingAt;

}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundFractureComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 BoneDamage;

    [DataField, AutoNetworkedField]
    public FractureGrade Grade;

    [DataField, AutoNetworkedField]
    public FractureTreatment Treatment;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundScarComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<WoundPrototype> SourcePrototype;

    [DataField, AutoNetworkedField]
    public FixedPoint2 SourcePeakSeverity;

}

[Serializable, NetSerializable]
public enum WoundState : byte
{
    Open,
    Stabilized,
    Closed,
    Healed,
    Scarred,
}

[Serializable, NetSerializable]
public enum BleedingTreatment : byte
{
    None,
    Bandaged,
    Clamped,
    Sutured,
    Cauterized,
}

[Serializable, NetSerializable]
public enum FractureGrade : byte
{
    None,
    Hairline,
    Simple,
    Displaced,
    Comminuted,
}

[Serializable, NetSerializable]
public enum FractureTreatment : byte
{
    None,
    Reduced,
    Mended,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SystemicDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();
}
