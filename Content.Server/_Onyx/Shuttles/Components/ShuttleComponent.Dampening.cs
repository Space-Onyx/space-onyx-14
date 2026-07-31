using Content.Shared._Onyx.Shuttles.Events;

namespace Content.Server.Shuttles.Components;

public sealed partial class ShuttleComponent
{
    [DataField]
    public InertiaDampeningMode DampeningMode = InertiaDampeningMode.Dampen;
}
