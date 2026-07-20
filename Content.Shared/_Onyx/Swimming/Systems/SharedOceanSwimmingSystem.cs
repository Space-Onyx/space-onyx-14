using Content.Shared._Onyx.Swimming.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Ghost;
using Content.Shared.Movement.Components;

namespace Content.Shared._Onyx.Swimming.Systems;

public sealed class SharedOceanSwimmingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OceanSwimmingComponent, CanWeightlessMoveEvent>(OnCanWeightlessMove);
    }

    public bool ShouldIgnoreOceanSwimming(EntityUid uid)
    {
        return HasComp<GhostComponent>(uid) || HasComp<CanMoveInAirComponent>(uid);
    }

    private void OnCanWeightlessMove(Entity<OceanSwimmingComponent> ent, ref CanWeightlessMoveEvent args)
    {
        args.CanMove = true;
    }
}