using Robust.Shared.Map;

namespace Content.Server._Onyx.Projectiles.TargetGuided;

[RegisterComponent]
public sealed partial class TargetGuidedComponent : Component
{
    [DataField] public Angle? TurnRate = Angle.FromDegrees(120);
    [DataField] public float Acceleration = 40f;
    [DataField] public float MaxSpeed = 20f;
    [DataField] public float LaunchSpeed = 8f;
    [DataField] public float MaxLifetime = 30f;
    [DataField] public float GuidanceTimeout = 1f;

    [ViewVariables] public EntityUid? ControllingConsole;
    [ViewVariables] public EntityCoordinates? TargetPosition;
    [ViewVariables] public float CurrentSpeed;
    [ViewVariables] public float Lifetime;
    [ViewVariables] public float TimeSinceGuidance;
    [ViewVariables] public Angle? FixedDirection;
}
