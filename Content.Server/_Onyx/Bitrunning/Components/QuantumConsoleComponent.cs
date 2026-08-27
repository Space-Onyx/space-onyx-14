using Content.Server._Onyx.Bitrunning.Systems;

namespace Content.Server._Onyx.Bitrunning.Components;

[RegisterComponent]
public sealed partial class QuantumConsoleComponent : Component
{
    [Access(typeof(QuantumConsoleSystem))]
    public EntityUid? LinkedServerId;
}
