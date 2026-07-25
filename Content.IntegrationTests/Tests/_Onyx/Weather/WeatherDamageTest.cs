using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Onyx.Weather;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Onyx.Weather;

[TestOf(typeof(WeatherDamageSystem))]
public sealed class WeatherDamageTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestOnyxWeatherTarget
  components:
  - type: MobState
  - type: Damageable
";

    [Test]
    [RunOnSide(Side.Server)]
    public async Task ImmunityBlocksWeatherDamageUntilExpiry()
    {
        var maps = SEntMan.System<SharedMapSystem>();
        var weather = SEntMan.System<SharedWeatherSystem>();
        var statuses = SEntMan.System<StatusEffectsSystem>();
        var damage = SEntMan.System<DamageableSystem>();
        var map = maps.CreateMap(out var mapId);
        var exposed = SEntMan.SpawnEntity("TestOnyxWeatherTarget", new EntityCoordinates(map, 0, 0));
        var protectedTarget = SEntMan.SpawnEntity("TestOnyxWeatherTarget", new EntityCoordinates(map, 1, 0));

        Assert.That(statuses.TryAddStatusEffectDuration(protectedTarget,
            "StatusEffectOnyxWeatherImmunity",
            TimeSpan.FromSeconds(1.2)), Is.True);
        Assert.That(weather.TryAddWeather(mapId, "WeatherAshfall", out _, TimeSpan.FromSeconds(3)), Is.True);

        await RunSeconds(1.1f);
        Assert.Multiple(() =>
        {
            Assert.That(damage.GetTotalDamage((exposed, SComp<DamageableComponent>(exposed))), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(damage.GetTotalDamage((protectedTarget, SComp<DamageableComponent>(protectedTarget))), Is.Zero);
        });

        await RunSeconds(1.1f);
        Assert.That(damage.GetTotalDamage((protectedTarget, SComp<DamageableComponent>(protectedTarget))), Is.GreaterThan(FixedPoint2.Zero));
    }
}
