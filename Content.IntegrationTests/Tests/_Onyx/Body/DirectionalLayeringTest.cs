// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client._Onyx.Body;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Onyx.Body;

public sealed class DirectionalLayeringTest : InteractionTest
{
    protected override string PlayerPrototype => "MobVulpkanin";

    [Test]
    public async Task DirectionChangesPreserveLayersAndMappings()
    {
        await Client.WaitAssertion(() =>
        {
            var system = CEntMan.System<DirectionalLayeringSystem>();
            var sprites = CEntMan.System<SpriteSystem>();
            var sprite = CEntMan.GetComponent<SpriteComponent>(CPlayer);
            var layers = sprite.LayerData.ToArray();
            var keys = System.Enum.GetValues<HumanoidVisualLayers>()
                .Where(key => sprites.LayerMapTryGet(CPlayer, key, out _, false))
                .ToDictionary(key => key, key => sprite.LayerData[sprites.LayerMapGet(CPlayer, key)]);

            foreach (var direction in new[] { Direction.South, Direction.East, Direction.North,
                         Direction.West, Direction.South, Direction.North, Direction.South })
            {
                Assert.That(system.TryUpdate((CPlayer, sprite), Angle.Zero, direction), Is.True);
                Assert.That(sprite.LayerData, Is.EquivalentTo(layers));
                foreach (var (key, layer) in keys)
                    Assert.That(sprite.LayerData[sprites.LayerMapGet(CPlayer, key)], Is.SameAs(layer));

                var hair = sprites.LayerMapGet(CPlayer, HumanoidVisualLayers.Hair);
                var ears = sprites.LayerMapGet(CPlayer, HumanoidVisualLayers.HeadTop);
                Assert.That(direction == Direction.North ? hair > ears : hair < ears, Is.True);

                var ordered = sprite.LayerData.ToArray();
                system.TryUpdate((CPlayer, sprite), Angle.Zero, direction);
                Assert.That(sprite.LayerData, Is.EqualTo(ordered));
            }
        });
    }
}
