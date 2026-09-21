// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.Client._Onyx.Overlays;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Onyx.Overlays;

[TestFixture]
[TestOf(typeof(EntityOcclusionOverlay))]
public sealed class EntityOcclusionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: EntityOcclusionAabbTarget
  components:
  - type: Physics
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.35,-0.35,0.35,0.35""
        hard: true

- type: entity
  id: EntityOcclusionPolygonTarget
  components:
  - type: Physics
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PolygonShape
          vertices:
          - ""-0.35,-0.35""
          - ""0.35,-0.35""
          - ""0.35,0.35""
          - ""-0.35,0.35""
        hard: true
";

    private static readonly string[] TargetPrototypes =
    [
        "MobHuman",
        "EntityOcclusionAabbTarget",
        "EntityOcclusionPolygonTarget",
    ];

    [Test]
    public async Task DiagonalCornerVisibilityIsRotationInvariant()
    {
        var map = await Pair.CreateTestMap();
        var cases = new List<VisibilityCase>();

        await Server.WaitPost(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            for (var targetIndex = 0; targetIndex < TargetPrototypes.Length; targetIndex++)
            {
                for (var rotationIndex = 0; rotationIndex < 4; rotationIndex++)
                {
                    var rotation = Angle.FromDegrees(rotationIndex * 90);
                    var origin = new Vector2(rotationIndex * 8, targetIndex * 8);
                    var wall = SEntMan.SpawnEntity("WallSolidDiagonal", new MapCoordinates(origin, map.MapId));
                    transform.SetWorldRotation(wall, rotation);

                    var target = SEntMan.SpawnEntity(
                        TargetPrototypes[targetIndex],
                        new MapCoordinates(origin + rotation.RotateVec(new Vector2(1f, 0.4f)), map.MapId));
                    cases.Add(new VisibilityCase(
                        SEntMan.GetNetEntity(target),
                        new MapCoordinates(origin + rotation.RotateVec(new Vector2(-2f, 0f)), map.MapId),
                        true,
                        $"{TargetPrototypes[targetIndex]} visible at {rotationIndex * 90} degrees"));
                }
            }
        });

        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() => AssertCases(cases));
    }

    [Test]
    public async Task WallsHideOnlyFullyCoveredTargets()
    {
        var map = await Pair.CreateTestMap();
        var cases = new List<VisibilityCase>();

        await Server.WaitPost(() =>
        {
            for (var targetIndex = 0; targetIndex < TargetPrototypes.Length; targetIndex++)
            {
                var diagonalOrigin = new Vector2(0f, targetIndex * 8f);
                SEntMan.SpawnEntity("WallSolidDiagonal", new MapCoordinates(diagonalOrigin, map.MapId));
                var diagonalTarget = SEntMan.SpawnEntity(
                    TargetPrototypes[targetIndex],
                    new MapCoordinates(diagonalOrigin + new Vector2(1f, 0f), map.MapId));
                cases.Add(new VisibilityCase(
                    SEntMan.GetNetEntity(diagonalTarget),
                    new MapCoordinates(diagonalOrigin + new Vector2(-2f, 0f), map.MapId),
                    false,
                    $"{TargetPrototypes[targetIndex]} fully covered by diagonal wall"));

                var squareOrigin = new Vector2(8f, targetIndex * 8f);
                SEntMan.SpawnEntity("WallSolid", new MapCoordinates(squareOrigin, map.MapId));
                var squareTarget = SEntMan.SpawnEntity(
                    TargetPrototypes[targetIndex],
                    new MapCoordinates(squareOrigin + new Vector2(1f, 0.4f), map.MapId));
                cases.Add(new VisibilityCase(
                    SEntMan.GetNetEntity(squareTarget),
                    new MapCoordinates(squareOrigin + new Vector2(-2f, 0f), map.MapId),
                    false,
                    $"{TargetPrototypes[targetIndex]} covered by ordinary wall"));
            }
        });

        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() => AssertCases(cases));
    }

    private void AssertCases(List<VisibilityCase> cases)
    {
        var overlay = new EntityOcclusionOverlay();
        try
        {
            foreach (var testCase in cases)
            {
                var target = CEntMan.GetEntity(testCase.Target);
                var xform = CEntMan.GetComponent<TransformComponent>(target);
                Assert.That(
                    overlay.IsVisible(testCase.Eye, target, xform),
                    Is.EqualTo(testCase.Expected),
                    testCase.Description);
            }
        }
        finally
        {
            overlay.Dispose();
        }
    }

    private readonly record struct VisibilityCase(
        NetEntity Target,
        MapCoordinates Eye,
        bool Expected,
        string Description);
}
