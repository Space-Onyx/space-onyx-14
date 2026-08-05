using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared._Onyx.Surgery.Augments;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Surgery.Augments;

/// <summary>
/// Transfers energy from an augment power network into a consumer's own battery.
/// </summary>
public sealed partial class AugmentBatteryChargerSystem : EntitySystem
{
    [Dependency] private AugmentSystem _augment = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<AugmentBatteryChargerComponent, AugmentPowerReceiverComponent, BatteryComponent>();
        while (query.MoveNext(out var uid, out var charger, out var receiver, out var battery))
        {
            var missing = battery.MaxCharge - _battery.GetCharge((uid, battery));
            var amount = MathF.Min(missing, charger.ChargeRate * (float) UpdateInterval.TotalSeconds);
            if (amount <= 0f || !_augment.TryUsePower((uid, receiver), amount))
                continue;

            _battery.ChangeCharge((uid, battery), amount);
        }
    }
}
