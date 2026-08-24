using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Body;
using Content.Shared.Body.Part;

namespace Content.Shared._Onyx.Body;

/// <summary>
/// Raised on a body after its organs changed, used to trigger systems that depend on the whole organ graph.
/// </summary>
[ByRefEvent]
public readonly record struct BodyOrgansChangedEvent(EntityUid Body);

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyAnatomyComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<BodyPartType, int> RequiredParts = new();

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, int> RequiredOrgans = new();

    [DataField, AutoNetworkedField]
    public bool AnatomyInitialized;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MissingEarsComponent : Component;

[RegisterComponent]
public sealed partial class MissingEyesComponent : Component;

/// <summary>
/// Prevents breathing and suffocation damage.
/// </summary>
[RegisterComponent]
public sealed partial class BreathingImmunityComponent : Component;

[RegisterComponent]
public sealed partial class MissingHeadComponent : Component
{
    public float Elapsed;
}

[RegisterComponent]
public sealed partial class InitiallyLungedComponent : Component;
