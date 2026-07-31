using Content.Server._Onyx.FireControl;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Spawners;

namespace Content.Server._Onyx.Radar;

public sealed partial class HitscanRadarSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanRaycastFiredEvent>(OnFired);
    }

    private void OnFired(Entity<HitscanBasicRaycastComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (!HasComp<FireControllableComponent>(args.Data.Gun))
            return;

        var gunCoordinates = Transform(args.Data.Gun).Coordinates;
        var from = _transform.ToMapCoordinates(gunCoordinates);
        var end = args.Data.HitEntity is { } hit
            ? _transform.GetMapCoordinates(hit).Position
            : from.Position + args.Data.ShotDirection * hitscan.Comp.MaxDistance;
        var line = Spawn(null, gunCoordinates);
        var component = EnsureComp<HitscanRadarComponent>(line);
        component.Start = from.Position;
        component.End = end;
        EnsureComp<TimedDespawnComponent>(line).Lifetime = 0.5f;
    }
}
