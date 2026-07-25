using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Temperature.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Temperature.Components;

namespace Content.IntegrationTests.Tests._Onyx.StatusEffects.Immunities;

[TestOf(typeof(FlammableSystem)), TestOf(typeof(TemperatureSystem))]
public sealed class FireTemperatureImmunityTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestOnyxImmunityTarget
  components:
  - type: Flammable
    damage: {}
  - type: Temperature
";

    [Test]
    [RunOnSide(Side.Server)]
    public async Task BlocksFireAndHeatUntilExpiry()
    {
        var target = SSpawn("TestOnyxImmunityTarget");
        var flammable = SComp<FlammableComponent>(target);
        var temperature = SComp<TemperatureComponent>(target);
        var fire = SEntMan.System<FlammableSystem>();
        var heat = SEntMan.System<TemperatureSystem>();
        var statuses = SEntMan.System<StatusEffectsSystem>();

        fire.SetFireStacks(target, 2, flammable, ignite: true);
        Assert.That(flammable.OnFire, Is.True);

        Assert.That(statuses.TryAddStatusEffectDuration(target,
            "StatusEffectOnyxFireImmunity",
            TimeSpan.FromSeconds(0.1)), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(flammable.OnFire, Is.False);
            Assert.That(flammable.FireStacks, Is.Zero);
        });

        fire.SetFireStacks(target, 2, flammable, ignite: true);
        heat.ForceChangeTemperature(target, Atmospherics.T20C + 100, temperature);
        Assert.Multiple(() =>
        {
            Assert.That(flammable.OnFire, Is.False);
            Assert.That(temperature.CurrentTemperature, Is.EqualTo(Atmospherics.T20C));
        });

        await RunSeconds(0.2f);
        fire.SetFireStacks(target, 2, flammable, ignite: true);
        heat.ForceChangeTemperature(target, Atmospherics.T20C + 100, temperature);
        Assert.Multiple(() =>
        {
            Assert.That(flammable.OnFire, Is.True);
            Assert.That(temperature.CurrentTemperature, Is.EqualTo(Atmospherics.T20C + 100));
        });
    }
}
