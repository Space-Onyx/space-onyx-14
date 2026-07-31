using Content.Shared.Body;
using Content.Shared.Humanoid.Prototypes;
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

/// <summary>Maps a Corvax external organ to a surgical body part without replacing its organ system.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BodyPartComponent : Component
{
    public const string PartSlotPrefix = "body_part_slot_";
    public const string OrganSlotPrefix = "body_organ_slot_";
    /// <summary>Owning body. Mirrored from the legacy OrganComponent during migration.</summary>
    [DataField, AutoNetworkedField] public EntityUid? Body;

    /// <summary>
    /// Logical parent in the RMC-compatible part graph.
    /// Corvax stores external organs flat, so the graph is reconstructed from their categories.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid? Parent;

    [DataField, AutoNetworkedField] public Dictionary<string, BodyPartType> Children = new();
    [DataField, AutoNetworkedField] public Dictionary<string, BodyPartSlot> ChildSlots = new();
    [DataField, AutoNetworkedField] public HashSet<string> Organs = new();

    [DataField, AutoNetworkedField] public BodyPartType PartType = BodyPartType.Other;
    [DataField, AutoNetworkedField] public BodyPartSymmetry Symmetry = BodyPartSymmetry.None;
    [DataField("vital"), AutoNetworkedField] public bool IsVital;
    [DataField, AutoNetworkedField] public ProtoId<SpeciesPrototype>? Species;

    /// <summary>Legacy visual category retained while Corvax visual bodies migrate to graph queries.</summary>
    [DataField, AutoNetworkedField] public ProtoId<OrganCategoryPrototype>? Category;
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
