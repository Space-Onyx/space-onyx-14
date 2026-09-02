using System.Linq;
using Content.Shared.Body;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared._Onyx.Surgery.Augments;

namespace Content.Server._Onyx.Surgery.Augments;

public sealed partial class AugmentReactorSystem : EntitySystem
{
    private const float UpdateInterval = 0.25f;

    [Dependency] private AugmentSystem _augment = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SatiationSystem _satiation = default!;

    private float _accumulator;

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        var elapsed = _accumulator;
        _accumulator = 0f;
        var query = EntityQueryEnumerator<AugmentReactorComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var reactor, out var organ))
        {
            if (organ.Body is not { } body || reactor.Generation <= 0f ||
                TryComp(uid, out CyberneticsComponent? cybernetics) && cybernetics.Disabled)
            {
                SetGeneration(uid, reactor, 0f);
                continue;
            }

            var batteries = _augment.GetBatteries(body)
                .Where(battery => _battery.GetCharge(battery.AsNullable()) < battery.Comp.MaxCharge)
                .ToList();
            if (batteries.Count == 0)
            {
                SetGeneration(uid, reactor, 0f);
                continue;
            }

            var generated = reactor.Generation * elapsed;
            Entity<SatiationComponent>? satiation = null;
            if (reactor.HungerCostPerJoule > 0f)
            {
                if (!TryComp(body, out SatiationComponent? component))
                {
                    SetGeneration(uid, reactor, 0f);
                    continue;
                }
                satiation = (body, component);
                var hungerCost = generated * reactor.HungerCostPerJoule;
                if (!_satiation.IsValueInRange(satiation.Value, SatiationSystem.Hunger,
                        above: reactor.MinimumHunger, hypotheticalValueDelta: -hungerCost))
                {
                    SetGeneration(uid, reactor, 0f);
                    continue;
                }
            }

            var remaining = generated;
            foreach (var battery in batteries)
            {
                var missing = battery.Comp.MaxCharge - _battery.GetCharge(battery.AsNullable());
                var charge = Math.Min(remaining, missing);
                if (charge <= 0f)
                    continue;
                _battery.ChangeCharge(battery.AsNullable(), charge);
                remaining -= charge;
                if (remaining <= 0f)
                    break;
            }

            var actual = generated - remaining;
            SetGeneration(uid, reactor, actual / elapsed);
            if (actual > 0f && satiation != null)
                _satiation.ModifyValue(satiation.Value, SatiationSystem.Hunger, -actual * reactor.HungerCostPerJoule);
        }
    }

    private void SetGeneration(EntityUid uid, AugmentReactorComponent reactor, float generation)
    {
        if (MathHelper.CloseTo(reactor.CurrentGeneration, generation))
            return;
        reactor.CurrentGeneration = generation;
        Dirty(uid, reactor);
    }
}
