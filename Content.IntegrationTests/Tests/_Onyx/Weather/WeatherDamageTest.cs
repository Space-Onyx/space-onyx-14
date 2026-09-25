using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Onyx.Weather;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weather;

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
        var weather = SEntMan.System<SharedWeatherSystem>();
        var statuses = SEntMan.System<StatusEffectsSystem>();
        var damage = SEntMan.System<DamageableSystem>();
        var map = await Pair.CreateTestMap();
        var exposed = SSpawnAtPosition("TestOnyxWeatherTarget", map.GridCoords);
        var protectedTarget = SSpawnAtPosition("TestOnyxWeatherTarget", map.GridCoords.Offset(new Vector2(1, 0)));

        Assert.That(statuses.TryAddStatusEffectDuration(protectedTarget,
            "StatusEffectOnyxWeatherImmunity",
            TimeSpan.FromSeconds(1.2)), Is.True);
        Assert.That(weather.TryAddWeather(map.MapId, "WeatherAshfall", out _, TimeSpan.FromSeconds(3)), Is.True);

        await RunSeconds(1.1f);
        Assert.Multiple(() =>
        {
            Assert.That(damage.GetTotalDamage((exposed, SComp<DamageableComponent>(exposed))), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(damage.GetTotalDamage((protectedTarget, SComp<DamageableComponent>(protectedTarget))),
                Is.EqualTo(FixedPoint2.Zero));
        });

        await RunSeconds(1.1f);
        Assert.That(damage.GetTotalDamage((protectedTarget, SComp<DamageableComponent>(protectedTarget))), Is.GreaterThan(FixedPoint2.Zero));
    }
}
