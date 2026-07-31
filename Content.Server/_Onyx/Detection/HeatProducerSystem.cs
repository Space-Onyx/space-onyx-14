using Content.Shared._Onyx.Detection;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Detection;

public sealed partial class HeatProducerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        var producers = EntityQueryEnumerator<HeatProducerComponent>();
        while (producers.MoveNext(out var uid, out var producer))
        {
            if (!producer.Enabled || producer.HeatPerSecond <= 0f)
                continue;

            var signature = EnsureComp<ThermalSignatureComponent>(uid);
            signature.StoredHeat += producer.HeatPerSecond;
        }

        var gridHeat = new Dictionary<EntityUid, float>();
        var signatures = EntityQueryEnumerator<ThermalSignatureComponent, TransformComponent>();
        while (signatures.MoveNext(out var uid, out var signature, out var xform))
        {
            signature.StoredHeat = MathF.Max(0f, signature.StoredHeat * Math.Clamp(signature.HeatRetention, 0f, 1f));
            if (xform.GridUid is { } grid && grid != uid)
                gridHeat[grid] = gridHeat.GetValueOrDefault(grid) + signature.StoredHeat;
        }

        var grids = EntityQueryEnumerator<MapGridComponent>();
        while (grids.MoveNext(out var uid, out _))
        {
            var ownHeat = TryComp<ThermalSignatureComponent>(uid, out var signature) ? signature.StoredHeat : 0f;
            var total = ownHeat + gridHeat.GetValueOrDefault(uid);
            if (total <= 0f && signature == null)
                continue;

            signature ??= EnsureComp<ThermalSignatureComponent>(uid);
            if (MathHelper.CloseTo(signature.AggregatedHeat, total))
                continue;

            signature.AggregatedHeat = total;
            Dirty(uid, signature);
        }
    }

    public void SetEnabled(Entity<HeatProducerComponent> producer, bool enabled)
    {
        producer.Comp.Enabled = enabled;
    }
}
