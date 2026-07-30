// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Resin;

public sealed partial class AreaSpawnerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private TransformSystem _transform = default!;

    private static readonly Vector2[] Neighbors = [Vector2.UnitY, -Vector2.UnitX, Vector2.UnitX, -Vector2.UnitY];

    public override void Initialize()
    {
        SubscribeLocalEvent<AreaSpawnerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<AreaSpawnerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var spawned in ent.Comp.Spawned)
        {
            if (TerminatingOrDeleted(spawned))
                continue;

            EnsureComp<TimedDespawnComponent>(spawned).Lifetime = _random.NextFloat(ent.Comp.MinTime, ent.Comp.MaxTime);
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<AreaSpawnerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Spawned.RemoveWhere(spawned => TerminatingOrDeleted(spawned));

            if (_timing.CurTime < comp.SpawnAt)
                continue;

            comp.SpawnAt = _timing.CurTime + comp.SpawnDelay;
            foreach (var offset in GetValidTiles(uid, comp))
                comp.Spawned.Add(Spawn(comp.SpawnPrototype, Transform(uid).Coordinates.Offset(offset)));
        }
    }

    private List<Vector2> GetValidTiles(EntityUid uid, AreaSpawnerComponent comp)
    {
        var result = new List<Vector2>();
        for (var y = -comp.Radius; y <= comp.Radius; y++)
        for (var x = -comp.Radius; x <= comp.Radius; x++)
        {
            var offset = new Vector2(x, y);
            if (IsValid(uid, comp, offset))
                result.Add(offset);
        }

        return result;
    }

    private bool IsValid(EntityUid uid, AreaSpawnerComponent comp, Vector2 offset)
    {
        var xform = Transform(uid);
        if (_transform.GetGrid((uid, xform)) is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid))
            return false;

        var coords = xform.Coordinates.Offset(offset);
        if (_turf.GetTileRef(coords) is not { } tile || tile.Tile.IsEmpty)
            return false;

        foreach (var entity in _map.GetAnchoredEntities((grid, mapGrid), coords))
        {
            if (TryComp<AirtightComponent>(entity, out var airtight) && airtight.AirBlocked || Prototype(entity) != null)
                return false;
        }

        foreach (var neighbor in Neighbors)
        foreach (var entity in _map.GetAnchoredEntities((grid, mapGrid), coords.Offset(neighbor)))
        {
            if (comp.Spawned.Contains(entity) || entity == uid)
                return true;
        }

        return false;
    }
}
