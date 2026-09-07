// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Clothing;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.Clothing;

public sealed partial class LooseDirtableSystem : EntitySystem
{
    private const string PuddleSolution = "puddle";

    [Dependency] private ClothingDirtSystem _dirt = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private readonly HashSet<Entity<ClothingDirtableComponent>> _dirtables = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PuddleComponent, PuddleDirtChangedEvent>(OnPuddleChanged);
        SubscribeLocalEvent<ClothingDirtableComponent, MoveEvent>(OnDirtableMoved);
    }

    private void OnPuddleChanged(Entity<PuddleComponent> ent, ref PuddleDirtChangedEvent args)
    {
        if (!_solutions.TryGetSolution(ent.Owner, PuddleSolution, out _, out var solution))
            return;

        _dirtables.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 0.45f, _dirtables,
            LookupFlags.Dynamic | LookupFlags.Sundries);
        foreach (var dirtable in _dirtables)
            TryApply(dirtable, solution);
    }

    private void OnDirtableMoved(Entity<ClothingDirtableComponent> ent, ref MoveEvent args)
    {
        if (args.OnlyRotation || _container.IsEntityInContainer(ent) || !args.NewPosition.IsValid(EntityManager))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.CoordinatesToTile(gridUid, grid, args.NewPosition);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!TryComp<SurfaceDirtSourceComponent>(uid, out var source) ||
                !_solutions.TryGetSolution(uid.Value, source.Solution, out _, out var solution))
                continue;
            TryApply(ent, solution, source.TransferAmount);
            return;
        }
    }

    private void TryApply(Entity<ClothingDirtableComponent> ent, Solution source,
        FixedPoint2 transferAmount = default)
    {
        if (transferAmount <= 0)
            transferAmount = FixedPoint2.New(1);
        if (!_container.IsEntityInContainer(ent))
            _dirt.TryDirtyClothing(ent, source, FixedPoint2.Min(source.Volume, transferAmount), ent.Comp);
    }
}
