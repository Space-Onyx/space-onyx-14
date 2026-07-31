using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Onyx.Detection;

public sealed partial class DetectionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private float _thermalMultiplier;
    private float _visualMultiplier;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.ShuttleThermalDetectionMultiplier, value => _thermalMultiplier = value, true);
        Subs.CVar(_cfg, CCVars.ShuttleVisualDetectionMultiplier, value => _visualMultiplier = value, true);
    }

    public DetectionLevel GetLevel(Entity<MapGridComponent> grid, EntityUid detector)
    {
        TryComp<DetectionRangeMultiplierComponent>(detector, out var detection);
        if (detection?.AlwaysDetect == true)
            return DetectionLevel.Detected;

        var bounds = grid.Comp.LocalAABB;
        var diagonal = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height);
        var visualRadius = diagonal * (detection?.VisualMultiplier ?? 1f) * _visualMultiplier;
        var heat = TryComp<ThermalSignatureComponent>(grid, out var signature)
            ? MathF.Max(signature.AggregatedHeat, 0f)
            : 0f;
        var thermalRadius = MathF.Sqrt(heat) * (detection?.ThermalMultiplier ?? 1f) * _thermalMultiplier;

        if (TryComp<DetectedAtRangeMultiplierComponent>(grid, out var visibility))
        {
            visualRadius = visualRadius * visibility.VisualMultiplier + visibility.VisualBias;
            thermalRadius *= visibility.ThermalMultiplier;
        }

        var detailedRadius = MathF.Max(visualRadius, thermalRadius * (detection?.ThermalOutlinePortion ?? 0.6f));
        if (!Transform(grid).Coordinates.TryDistance(EntityManager, Transform(detector).Coordinates, out var distance))
            return DetectionLevel.Undetected;

        if (distance <= detailedRadius)
            return DetectionLevel.Detected;
        return distance <= thermalRadius ? DetectionLevel.Partial : DetectionLevel.Undetected;
    }

    public MassLevel GetMassLevel(Entity<MapGridComponent> grid)
    {
        if (!TryComp<PhysicsComponent>(grid, out var physics))
            return MassLevel.Unknown;

        return physics.FixturesMass switch
        {
            >= 2000f => MassLevel.Supermassive,
            >= 1000f => MassLevel.Huge,
            >= 600f => MassLevel.Large,
            >= 300f => MassLevel.Medium,
            >= 0f => MassLevel.Small,
            _ => MassLevel.Unknown,
        };
    }

    public string GetMassLabel(Entity<MapGridComponent> grid)
    {
        return Loc.GetString($"shuttle-detection-mass-{GetMassLevel(grid).ToString().ToLowerInvariant()}");
    }

    public string GetLevelLabel(DetectionLevel level)
    {
        return Loc.GetString($"shuttle-detection-level-{level.ToString().ToLowerInvariant()}");
    }
}

public enum DetectionLevel : byte
{
    Detected,
    Partial,
    Undetected,
}

public enum MassLevel : byte
{
    Unknown,
    Small,
    Medium,
    Large,
    Huge,
    Supermassive,
}
