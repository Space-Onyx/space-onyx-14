using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
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
}
