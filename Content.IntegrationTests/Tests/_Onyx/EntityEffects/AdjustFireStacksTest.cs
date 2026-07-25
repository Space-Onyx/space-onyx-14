using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.EntityEffects.Effects.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.EntityEffects;

namespace Content.IntegrationTests.Tests._Onyx.EntityEffects;

[TestOf(typeof(AdjustFireStacks))]
public sealed class AdjustFireStacksTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestOnyxFlammable
  components:
  - type: Flammable
    damage: {}
";

    [Test]
    [RunOnSide(Side.Server)]
    public void AddsScaledStacksAndIgnites()
    {
        var uid = SSpawn("TestOnyxFlammable");
        var flammable = SComp<FlammableComponent>(uid);

        SEntMan.System<SharedEntityEffectsSystem>().ApplyEffect(uid, new AdjustFireStacks
        {
            Amount = 2f,
            Ignite = true,
        }, scale: 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(flammable.FireStacks, Is.EqualTo(3f));
            Assert.That(flammable.OnFire, Is.True);
        });
    }
}
