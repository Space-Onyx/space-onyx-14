// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.Client._Onyx.Overlays;
using Content.IntegrationTests.Fixtures;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Onyx.Overlays;

[TestFixture]
[TestOf(typeof(EntityOcclusionOverlay))]
public sealed class EntityOcclusionTest : GameTest
{
    [TestCase("Airlock")]
    [TestCase("Firelock")]
    [TestCase("WallSolid")]
    [TestCase("ShuttersNormal")]
    [TestCase("Bookshelf")]
    public async Task OccludingSurfacesAreNotClippedAgainstThemselves(string prototype)
    {
        var map = await Pair.CreateTestMap();
        NetEntity target = default;
        await Server.WaitPost(() =>
        {
            var uid = SEntMan.SpawnEntity(prototype, new MapCoordinates(Vector2.One, map.MapId));
            SEntMan.System<OccluderSystem>().SetEnabled(uid, true);
            target = SEntMan.GetNetEntity(uid);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var overlay = new EntityOcclusionOverlay();
            try
            {
                Assert.That(overlay.IsOccludingSurface(CEntMan.GetEntity(target)), Is.True);
            }
            finally
            {
                overlay.Dispose();
            }
        });

        await Server.WaitPost(() =>
            SEntMan.System<OccluderSystem>().SetEnabled(SEntMan.GetEntity(target), false));
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var overlay = new EntityOcclusionOverlay();
            try
            {
                Assert.That(overlay.IsOccludingSurface(CEntMan.GetEntity(target)), Is.False);
            }
            finally
            {
                overlay.Dispose();
            }
        });
    }

    [Test]
    public async Task FootprintsDoNotDependOnSpriteSizeOrColor()
    {
        var map = await Pair.CreateTestMap();
        NetEntity target = default;
        await Server.WaitPost(() =>
        {
            target = SEntMan.GetNetEntity(SEntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.One, map.MapId)));
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var uid = CEntMan.GetEntity(target);
            var xform = CEntMan.GetComponent<TransformComponent>(uid);
            var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
            var sprites = CEntMan.System<SpriteSystem>();
            var color = sprite.Color;
            var scale = sprite.Scale;
            var overlay = new EntityOcclusionOverlay();
            try
            {
                var before = new List<EntityOcclusionFootprint>();
                var after = new List<EntityOcclusionFootprint>();
                overlay.CollectFootprints(uid, xform, before);
                sprites.SetScale((uid, sprite), new Vector2(10f));
                overlay.CollectFootprints(uid, xform, after);
                Assert.That(before, Has.Count.GreaterThan(0));
                Assert.That(after, Has.Count.EqualTo(before.Count));
                for (var i = 0; i < before.Count; i++)
                {
                    Assert.That(after[i].Center, Is.EqualTo(before[i].Center));
                    Assert.That(after[i].Radius, Is.EqualTo(before[i].Radius));
                    Assert.That(after[i].Vertices, Is.EqualTo(before[i].Vertices));
                }
                overlay.RestoreAll();
                Assert.That(sprite.Color, Is.EqualTo(color));
            }
            finally
            {
                sprites.SetScale((uid, sprite), scale);
                overlay.Dispose();
            }
        });
    }
}
