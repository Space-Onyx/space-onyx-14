namespace Content.Server._Onyx.Projectiles.TargetSeeking;

/// <summary>
/// Allows a projectile to acquire and steer towards shuttle grids.
/// </summary>
[RegisterComponent]
public sealed partial class TargetSeekingComponent : Component
{
    [DataField]
    public float DetectionRange = 300f;

    [DataField]
    public Angle ScanArc = Angle.FromDegrees(360);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Angle? TurnRate = Angle.FromDegrees(100);

    [DataField]
    public EntityUid? CurrentTarget;

    [DataField]
    public TrackingMethod TrackingAlgorithm = TrackingMethod.Predictive;

    [DataField]
    public float Acceleration = 50f;

    [DataField]
    public float MaxSpeed = 50f;

    [DataField]
    public float LaunchSpeed = 10f;

    [DataField]
    public float CurrentSpeed;

    [DataField]
    public float FieldOfView = 90f;
}

public enum TrackingMethod
{
    Predictive = 1,
    Direct = 2,
}
