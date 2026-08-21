using Content.Shared.Body;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part;

[Serializable, NetSerializable]
public enum BodyPartType : ushort
{
    Other = 0,
    Torso = 1,
    Head = 2,
    Arm = 3,
    Hand = 4,
    Leg = 5,
    Foot = 6,
    Tail = 7,
    Chest = 8,
    Groin = 9
}

[Serializable, NetSerializable]
public enum BodyPartSymmetry { None, Left, Right }

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BodyPartComponent : Component
{
    public const string PartSlotPrefix = "body_part_slot_";
    public const string OrganSlotPrefix = "body_organ_slot_";
    [DataField, AutoNetworkedField] public EntityUid? Body;

    [DataField, AutoNetworkedField] public EntityUid? Parent;

    [DataField, AutoNetworkedField] public Dictionary<string, BodyPartType> Children = new();
    [DataField, AutoNetworkedField] public Dictionary<string, BodyPartSlot> ChildSlots = new();
    [DataField, AutoNetworkedField] public HashSet<string> Organs = new();

    [DataField, AutoNetworkedField] public BodyPartType PartType = BodyPartType.Other;
    [DataField, AutoNetworkedField] public BodyPartSymmetry Symmetry = BodyPartSymmetry.None;
    [DataField("vital"), AutoNetworkedField] public bool IsVital;
    [DataField, AutoNetworkedField] public ProtoId<SpeciesPrototype>? Species;

    [DataField, AutoNetworkedField] public ProtoId<OrganCategoryPrototype>? Category;

    /// <summary>Fracture profile for this part. Null = no fractures.</summary>
    [DataField]
    public ProtoId<FractureProfilePrototype>? FractureProfile;

    /// <summary>
    /// Maximum structural damage the part can take before further damage becomes overflow.
    /// Damage beyond the cap is not applied to the part (wounds, bleeding and pain stop
    /// growing once the part is destroyed), but instead accumulates as tear-off pressure.
    /// Once pressure reaches the cap the part becomes severable.
    /// </summary>
    [DataField]
    public FixedPoint2 MaxDamage;

    /// <summary>
    /// Damage thresholds per damage type. Once the part's total damage reaches
    /// the threshold (progress summed across damage types), the part becomes severable.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> AmputationThresholds = new();

    /// <summary>Minimum follow-up hit per structural damage type required to detach a ruined part.</summary>
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> DismembermentFinishingDamage = new();

    [DataField]
    public FixedPoint2 AmputationConsequenceSeverity = 35;

    [DataField]
    public FixedPoint2? DismembermentSeverity;
}

[DataDefinition]
[Serializable, NetSerializable]
public partial record struct BodyPartSlot
{
    [DataField(required: true)] public BodyPartType Type;
    [DataField] public BodyPartSymmetry Symmetry;

    public BodyPartSlot(BodyPartType type, BodyPartSymmetry symmetry)
    {
        Type = type;
        Symmetry = symmetry;
    }
}
