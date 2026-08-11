using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Fluids.EntitySystems;

public sealed partial class PuddleSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Indices)> _puddleTiles = new();

    private void InitializeBurningPuddles()
    {
        SubscribeLocalEvent<PuddleComponent, ComponentStartup>(OnBurningPuddleStartup);
        SubscribeLocalEvent<PuddleComponent, TileFireEvent>(OnBurningPuddleFire);
        SubscribeLocalEvent<PuddleComponent, MoveEvent>(OnBurningPuddleMoved);
        SubscribeLocalEvent<PuddleComponent, ComponentShutdown>(OnBurningPuddleShutdown);
    }

    private void OnBurningPuddleStartup(Entity<PuddleComponent> ent, ref ComponentStartup args)
    {
        UpdateBurningPuddleTile(ent);
    }

    protected override void OnPuddleBurningStateChanged(Entity<PuddleComponent> ent)
    {
        UpdateBurningPuddleTile(ent);
    }

    private void OnBurningPuddleMoved(Entity<PuddleComponent> ent, ref MoveEvent args)
    {
        UpdateBurningPuddleTile(ent);
    }

    private void OnBurningPuddleShutdown(Entity<PuddleComponent> ent, ref ComponentShutdown args)
    {
        if (!_puddleTiles.Remove(ent, out var oldTile))
            return;

        RecalculatePuddleFlammability(oldTile, ent);
    }

    private void OnBurningPuddleFire(Entity<PuddleComponent> ent, ref TileFireEvent args)
    {
        if (!_solutionContainerSystem.ResolveSolution(ent.Owner,
                ent.Comp.SolutionName,
                ref ent.Comp.Solution,
                out var solution))
        {
            return;
        }

        foreach (var reagent in solution.Contents.ToArray())
        {
            var flammability = ProtoMan.Index<ReagentPrototype>(reagent.Reagent.Prototype).Flammability;
            if (flammability <= 0)
                continue;

            var amount = Math.Ceiling(reagent.Quantity.Float() * 0.05f * flammability / 0.5f) * 0.5f;
            solution.RemoveReagent(reagent.Reagent, FixedPoint2.New((float) amount));
        }

        _solutionContainerSystem.UpdateChemicals(ent.Comp.Solution.Value);
    }

    private void UpdateBurningPuddleTile(Entity<PuddleComponent> ent)
    {
        _puddleTiles.TryGetValue(ent, out var oldTile);

        if (!TryGetPuddleTile(ent, out var newTile))
        {
            if (_puddleTiles.Remove(ent))
                RecalculatePuddleFlammability(oldTile, ent);
            return;
        }

        _puddleTiles[ent] = newTile;
        if (oldTile != default && oldTile != newTile)
            RecalculatePuddleFlammability(oldTile, ent);

        RecalculatePuddleFlammability(newTile);
    }

    private bool TryGetPuddleTile(EntityUid uid, out (EntityUid Grid, Vector2i Indices) tile)
    {
        tile = default;
        var xform = Transform(uid);
        if (!xform.Anchored ||
            xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        tile = (gridUid, _transform.GetGridTilePositionOrDefault((uid, xform), grid));
        return true;
    }

    private void RecalculatePuddleFlammability(
        (EntityUid Grid, Vector2i Indices) tile,
        EntityUid? excluded = null)
    {
        if (!TryComp<MapGridComponent>(tile.Grid, out var grid))
            return;

        var total = 0f;
        var anchored = _map.GetAnchoredEntitiesEnumerator(tile.Grid, grid, tile.Indices);
        while (anchored.MoveNext(out var uid))
        {
            if (uid == excluded ||
                TerminatingOrDeleted(uid.Value) ||
                !_puddleQuery.TryGetComponent(uid, out var puddle) ||
                !_solutionContainerSystem.ResolveSolution(uid.Value,
                    puddle.SolutionName,
                    ref puddle.Solution,
                    out var solution))
            {
                continue;
            }

            foreach (var reagent in solution.Contents)
            {
                total += ProtoMan.Index<ReagentPrototype>(reagent.Reagent.Prototype).Flammability *
                         reagent.Quantity.Float();
            }
        }

        _atmosphere.SetPuddleFlammability(tile.Grid, tile.Indices, total);
    }
}
