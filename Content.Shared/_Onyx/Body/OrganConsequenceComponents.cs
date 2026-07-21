using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Body;

[RegisterComponent, NetworkedComponent]
public sealed partial class MissingEarsComponent : Component;

[RegisterComponent]
public sealed partial class LungDependentComponent : Component;

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
