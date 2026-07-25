using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.EntityTable;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

public sealed class XenobiologyStationMapTest : GameTest
{
    private static readonly ProtoId<EntityTablePrototype> LockerFillTable = "FillLockerScienceXenobiology";

    private static readonly string[] RequiredPlacements =
    [
        "SlimeGrinder",
        "ComputerScienceXenobiologyBounty",
        "LockerScienceFilledXenobiology",
    ];

    private static readonly string[] RequiredLockerContents =
    [
        "ClothingBackpackXenoBioTankFilled",
        "SprayBottleWater",
        "SlimeScannerXenobio",
        "BoxSyringe",
        "PlasmaChemistryVial",
        "MonkeyCubeBox",
    ];

    [TestCase("/Maps/_Onyx/Stations/Origin/onyx_origin.yml")]
    [TestCase("/Maps/_Onyx/Stations/Omega/onyx_omega.yml")]
    [TestCase("/Maps/_Onyx/Stations/Kettle/onyx_kettle.yml")]
    [TestCase("/Maps/_Onyx/Stations/Kerberos/onyx_kerberos.yml")]
    [TestCase("/Maps/_Onyx/Stations/Gate/onyx_gate.yml")]
    [TestCase("/Maps/_Onyx/Stations/Box/onyx_box.yml")]
    [TestCase("/Maps/_Onyx/Stations/Beta/onyx_beta.yml")]
    [RunOnSide(Side.Server)]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task StationHasCompleteXenobiologyLoop(string path)
    {
        var mapLoader = Server.System<MapLoaderSystem>();
        var mapSystem = Server.System<SharedMapSystem>();
        MapId mapId = default;

        await Server.WaitAssertion(() =>
        {
            Assert.That(mapLoader.TryLoadMap(new ResPath(path), out var map, out var grids), Is.True, path);
            Assert.That(map, Is.Not.Null, path);
            Assert.That(grids, Is.Not.Empty, path);
            mapId = map!.Value.Comp.MapId;

            var placed = new HashSet<string>();
            var query = SEntMan.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out var metadata, out var transform))
            {
                if (transform.MapID == mapId && metadata.EntityPrototype is { } prototype)
                    placed.Add(prototype.ID);
            }

            foreach (var id in RequiredPlacements)
                Assert.That(placed, Does.Contain(id), $"{path}: missing {id}");

            Assert.That(placed.Contains("XenobioSlimeBabySpawner") || placed.Contains("MobSlimeXenobioBabyGrey"),
                Is.True,
                $"{path}: missing Grey starter");

            var locker = SProtoMan.Index(LockerFillTable);
            var contents = SEntMan.System<EntityTableSystem>().GetSpawns(locker)
                .Select(id => id.Id)
                .ToHashSet();

            foreach (var id in RequiredLockerContents)
                Assert.That(contents, Does.Contain(id), $"{path}: xenobiology locker missing {id}");

            foreach (var id in RequiredPlacements.Concat(RequiredLockerContents).Append("XenobioSlimeBabySpawner"))
                Assert.That(SProtoMan.TryIndex<EntityPrototype>(id, out _), Is.True, $"{path}: unknown prototype {id}");
        });

        await Server.WaitPost(() => mapSystem.DeleteMap(mapId));
    }
}
