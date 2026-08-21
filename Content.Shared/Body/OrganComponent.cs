using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes; // <Onyx-OrganDamage>
using Content.Shared.FixedPoint;

namespace Content.Shared.Body;

/// <summary>
/// Marks an entity as being able to be inserted into an entity with <seealso cref="BodyComponent" />.
/// </summary>
/// <seealso cref="BodySystem" />
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
// <Onyx-OrganDamage-edited>
[Access(typeof(BodySystem), typeof(InitialBodySystem), typeof(Systems.SharedBodySystem), typeof(_Onyx.Wounds.OrganDamageSystem), typeof(_Onyx.Medical.Surgery.SharedSurgerySystem), typeof(_Onyx.Body.Systems.OrganHealthSystem))]
// </Onyx-OrganDamage-edited>
public sealed partial class OrganComponent : Component
{
    // <Onyx-OrganHealth>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Health = FixedPoint2.New(15);

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxHealth = FixedPoint2.New(15);
    // </Onyx-OrganHealth>

    // <Onyx-OrganDamage>
    /// <summary>
    /// Multiplies the body part profile's chance for this organ to take damage.
    /// </summary>
    [DataField]
    public float DamageChanceMultiplier = 1f;

    /// <summary>
    /// Multiplies this organ's profile selection weight.
    /// </summary>
    [DataField]
    public float SelectionMultiplier = 1f;

    /// <summary>
    /// Per-damage-type vulnerability. Missing damage types default to 1.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> DamageMultipliers = new();

    /// <summary>
    /// Optional wound created on the containing part when this organ is destroyed.
    /// </summary>
    [DataField]
    public ProtoId<_Onyx.Wounds.WoundPrototype>? DestructionWound;

    [DataField]
    public FixedPoint2 DestructionWoundSeverity;
    // </Onyx-OrganDamage>

    /// <summary>
    /// The body entity containing this organ, if any
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    /// What kind of organ is this, if any
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Category;

    // <Onyx-OrganEffects>
    /// <summary>
    /// Components added to the body when this organ is inserted and removed when it is taken out.
    /// </summary>
    [DataField]
    public ComponentRegistry? OnAdd;
    // </Onyx-OrganEffects>
}
