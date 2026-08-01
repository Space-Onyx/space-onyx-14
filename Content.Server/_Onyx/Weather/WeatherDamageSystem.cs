using Content.Shared._Onyx.StatusEffects.Immunities;
using Content.Shared._Onyx.Weather;
using Content.Shared._Onyx.Salvage.Weapons;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Weather;

public sealed partial class WeatherDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundDamageRoutingSystem _woundDamageRouting = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private StatusEffectsSystem _statuses = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;

    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var weatherQuery = EntityQueryEnumerator<WeatherDamageComponent, StatusEffectComponent>();
        while (weatherQuery.MoveNext(out _, out var weatherDamage, out var status))
        {
            if (!status.Applied || status.AppliedTo is not { } map || now < weatherDamage.NextUpdate)
                continue;

            weatherDamage.NextUpdate = now + weatherDamage.UpdateInterval;
            DamageExposedMobs(map, weatherDamage.Damage);
        }
    }

    private void DamageExposedMobs(EntityUid map, Content.Shared.Damage.DamageSpecifier damage)
    {
        var mobQuery = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var uid, out var mob, out var transform))
        {
            if (transform.MapUid != map || mob.CurrentState == MobState.Dead ||
                HasComp<WeatherImmuneComponent>(uid) ||
                HasComp<FaunaComponent>(uid) ||
                _statuses.HasEffectComp<WeatherImmunityStatusEffectComponent>(uid))
                continue;

            if (transform.GridUid is { } gridUid && _gridQuery.TryComp(gridUid, out var grid))
            {
                var tile = _maps.GetTileRef((gridUid, grid), transform.Coordinates);
                if (!_weather.CanWeatherAffect((gridUid, grid, null), tile))
                    continue;
            }

            if (!HasComp<WoundHostComponent>(uid) ||
                !_woundDamageRouting.TryApplyDistributedDamage(uid, damage, TargetBodyPart.All,
                    DamageDistribution.SplitByPartWeight, interruptsDoAfters: false)) // <Onyx-WeatherWounds>
            {
                _damageable.TryChangeDamage(uid, damage, interruptsDoAfters: false);
            }
        }
    }
}
