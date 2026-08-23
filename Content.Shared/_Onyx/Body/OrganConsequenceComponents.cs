using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Body;

/// <summary>
/// Raised on a body after its organs changed, used to trigger systems that depend on the whole organ graph.
/// </summary>
[ByRefEvent]
public readonly record struct BodyOrgansChangedEvent(EntityUid Body);

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
public sealed partial class InitiallyLeggedComponent : Component
{
    public int InitialLegCount;
}

[RegisterComponent]
public sealed partial class InitiallyLungedComponent : Component
{
    public int InitialLungCount;
}
