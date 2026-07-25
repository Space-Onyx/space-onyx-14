using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.EntityEffects.Effects.Transform;
using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Onyx.EntityEffects;

[TestOf(typeof(TeleportNearby))]
public sealed class TeleportNearbyTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestOnyxTeleportTarget
  components:
  - type: MobState
  - type: Physics
";

    [Test]
    [RunOnSide(Side.Server)]
    public async Task TeleportsNearbyMob()
    {
        var map = await Pair.CreateTestMap();
        var source = SEntMan.SpawnEntity(null, map.GridCoords);
        var target = SEntMan.SpawnEntity("TestOnyxTeleportTarget", map.GridCoords.Offset(new Vector2(0.1f, 0f)));
        var before = SComp<TransformComponent>(target).Coordinates;

        SEntMan.System<SharedEntityEffectsSystem>().ApplyEffect(source, new TeleportNearby
        {
            Range = 1f,
            Radius = new Vector2(0.25f, 0.25f),
            Attempts = 1,
        });

        Assert.That(SComp<TransformComponent>(target).Coordinates, Is.Not.EqualTo(before));
    }
}
