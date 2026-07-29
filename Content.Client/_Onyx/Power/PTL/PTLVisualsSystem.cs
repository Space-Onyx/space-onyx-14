using Content.Shared._Onyx.Power.PTL;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Power.PTL;

public sealed partial class PTLVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _time = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<PTLVisualsComponent>();
        while (query.MoveNext(out var uid, out var visuals))
            UpdateVisuals((uid, visuals));
    }

    private void UpdateVisuals(Entity<PTLVisualsComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || !TryComp<PTLComponent>(ent, out var ptl))
            return;

        _sprite.LayerSetVisible((ent.Owner, sprite), PTLVisualLayers.Unpowered, !ptl.Active);
        _sprite.LayerSetVisible((ent.Owner, sprite), PTLVisualLayers.Charge, ptl.Active);
        var remaining = (ptl.NextShotAt - _time.CurTime).Seconds;
        var state = Math.Clamp(remaining / ptl.ShootDelay * ent.Comp.MaxChargeStates, 1, ent.Comp.MaxChargeStates);
        _sprite.LayerSetRsiState((ent.Owner, sprite), PTLVisualLayers.Charge, $"{ent.Comp.ChargePrefix}{(int) state}");
    }
}

enum PTLVisualLayers : byte
{
    Base,
    Unpowered,
    Charge,
}
