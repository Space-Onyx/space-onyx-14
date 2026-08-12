using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.CloneProjector;

[RegisterComponent]
public sealed partial class GhostRolePlaytimeRequirementComponent : Component
{
    [DataField(required: true)]
    public ProtoId<PlayTimeTrackerPrototype> Tracker;

    [DataField(required: true)]
    public TimeSpan Time;
}
