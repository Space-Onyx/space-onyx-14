using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server.Gravity;
using Content.Shared._Onyx.Clothing;
using Content.Shared._Onyx.Footprints;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Footprints;

public sealed partial class FootprintSystem : EntitySystem
{
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private ClothingDirtSystem _clothingDirt = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private const string FootprintSolution = "print";
    private const string PuddleSolution = "puddle";
    private static readonly EntProtoId FootprintPrototype = "Footprint";
    private static readonly FixedPoint2 MaxTileVolume = 50;
    private static readonly FixedPoint2 WornResidual = FixedPoint2.New(0.1f);
    private static readonly FixedPoint2 WornTransfer = FixedPoint2.New(0.1f);
    private float _minimumPuddleSize;

    public override void Initialize()
    {
        SubscribeLocalEvent<FootprintComponent, FootprintCleanEvent>(OnClean);
        SubscribeLocalEvent<FootprintOwnerComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<PuddleComponent, MapInitEvent>(OnPuddleMapInit);
        Subs.CVar(_configuration, CCVars.MinimumPuddleSizeForFootprints,
            value => _minimumPuddleSize = value, true);
    }

    private void OnClean(Entity<FootprintComponent> ent, ref FootprintCleanEvent args)
    {
        ToPuddle(ent);
    }

    private void OnMove(Entity<FootprintOwnerComponent> ent, ref MoveEvent args)
    {
        if (args.ParentChanged)
        {
            ent.Comp.Distance = 0;
            return;
        }

        if (args.OnlyRotation || _gravity.IsWeightless(ent.Owner) ||
            !args.OldPosition.IsValid(EntityManager) || !args.NewPosition.IsValid(EntityManager))
            return;

        var oldPosition = _transform.ToMapCoordinates(args.OldPosition).Position;
        var newPosition = _transform.ToMapCoordinates(args.NewPosition).Position;
        ent.Comp.Distance += Vector2.Distance(newPosition, oldPosition);

        var standing = TryComp<StandingStateComponent>(ent, out var state) && state.Standing;
        var requiredDistance = standing ? ent.Comp.FootDistance : ent.Comp.BodyDistance;
        if (requiredDistance <= 0 || ent.Comp.Distance < requiredDistance)
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        ent.Comp.Distance %= requiredDistance;
        EntityCoordinates coordinates = new(ent, standing ? ent.Comp.NextFootOffset : 0, 0);
        ent.Comp.NextFootOffset = -ent.Comp.NextFootOffset;
        var tile = _map.CoordinatesToTile(gridUid, grid, coordinates);
        if (TryPuddleInteraction(ent, (gridUid, grid), tile, standing))
            return;

        var rotation = standing
            ? xform.LocalRotation
            : (_map.WorldToLocal(gridUid, grid, newPosition) - _map.WorldToLocal(gridUid, grid, oldPosition)).ToAngle();
        LeavePrint(ent, (gridUid, grid), tile, coordinates, rotation, standing);
    }

    private bool TryPuddleInteraction(Entity<FootprintOwnerComponent> ent, Entity<MapGridComponent> grid,
        Vector2i tile, bool standing)
    {
        if (!TryGetAnchoredEntity<PuddleComponent>(grid, tile, out var puddle) ||
            !_solution.TryGetSolution(puddle.Value.Owner, PuddleSolution, out var puddleEnt, out var puddleSolution))
            return false;

        if (puddleSolution.Volume < FixedPoint2.New(_minimumPuddleSize))
            return false;

        var amount = FixedPoint2.Min(puddleSolution.Volume, FixedPoint2.New(1));
        if (standing)
        {
            _clothingDirt.TryDirtyWornPuddleStep(ent, puddleSolution, amount);
            if (HasWornItem(ent, SlotFlags.FEET) || HasWornItem(ent, SlotFlags.SOCKS))
                return true;
        }
        else
        {
            _clothingDirt.TryDirtyWornPuddleCrawl(ent, puddleSolution, amount);
            if (HasWornItem(ent, SlotFlags.FEET) || HasWornItem(ent, SlotFlags.SOCKS))
                return true;
        }

        if (!_solution.TryGetSolution(ent.Owner, FootprintSolution, out var ownerEnt, out var ownerSolution))
            return false;

        _solution.TryTransferSolution(puddleEnt.Value, ownerSolution, GetPrintVolume(ent, (ownerEnt.Value, ownerEnt.Value.Comp)));
        _solution.TryTransferSolution(ownerEnt.Value, puddleSolution,
            FixedPoint2.Max(0, (standing ? ent.Comp.MaxFootVolume : ent.Comp.MaxBodyVolume) - ownerSolution.Volume));
        return true;
    }

    private void LeavePrint(Entity<FootprintOwnerComponent> ent, Entity<MapGridComponent> grid, Vector2i tile,
        EntityCoordinates coordinates, Angle rotation, bool standing)
    {
        if (!TryGetSource(ent, standing, out var source, out var clothing))
            return;

        var volume = standing ? GetPrintVolume(ent, source.Value) : GetBodyprintVolume(ent, source.Value);
        if (clothing)
        {
            var available = source.Value.Comp.Solution.Volume - WornResidual;
            if (available <= 0)
                return;
            volume = FixedPoint2.Min(WornTransfer, available);
        }
        else if (volume < ent.Comp.MinFootprintVolume)
        {
            _solution.RemoveAllSolution(source.Value);
            return;
        }

        if (!TryGetAnchoredEntity<FootprintComponent>(grid, tile, out var footprint))
        {
            var uid = SpawnAtPosition(FootprintPrototype, coordinates);
            footprint = (uid, Comp<FootprintComponent>(uid));
        }

        if (!_solution.TryGetSolution(footprint.Value.Owner, FootprintSolution, out var footprintEnt, out var footprintSolution))
            return;

        var visualVolume = clothing ? FixedPoint2.Max(volume, FixedPoint2.New(ent.Comp.MinFootprintVolume)) : volume;
        var maxVisual = standing ? ent.Comp.MaxFootprintVolume : ent.Comp.MaxBodyprintVolume;
        var color = source.Value.Comp.Solution.GetColor(_prototype).WithAlpha(visualVolume.Float() / maxVisual / 2f);
        _solution.TryTransferSolution(footprintEnt.Value, source.Value.Comp.Solution, volume);

        if (footprintSolution.Volume >= MaxTileVolume)
        {
            var solution = footprintSolution.Clone();
            if (_puddle.TrySpillAt(coordinates, solution, out _, false))
                Del(footprint.Value.Owner);
            return;
        }

        var gridCoords = _map.LocalToGrid(grid, grid, coordinates);
        var x = gridCoords.X / grid.Comp.TileSize;
        var y = gridCoords.Y / grid.Comp.TileSize;
        x -= MathF.Floor(x) + 0.5f;
        y -= MathF.Floor(y) + 0.5f;
        footprint.Value.Comp.Footprints.Add(new(new(x, y), rotation, color, standing ? "foot" : "body"));
        Dirty(footprint.Value);
    }

    private void OnPuddleMapInit(Entity<PuddleComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<FootprintComponent>(ent))
            return;
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;
        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        if (TryGetAnchoredEntity<FootprintComponent>((gridUid, grid), tile, out var footprint))
            ToPuddle(footprint.Value.Owner, xform.Coordinates);
    }

    private void ToPuddle(EntityUid uid, EntityCoordinates? coordinates = null)
    {
        coordinates ??= Transform(uid).Coordinates;
        if (!_solution.TryGetSolution(uid, FootprintSolution, out _, out var solution))
            return;
        var clone = solution.Clone();
        if (_puddle.TrySpillAt(coordinates.Value, clone, out _, false))
            Del(uid);
    }

    private static FixedPoint2 GetPrintVolume(Entity<FootprintOwnerComponent> ent, Entity<SolutionComponent> solution)
        => FixedPoint2.Min(solution.Comp.Solution.Volume,
            (ent.Comp.MaxFootprintVolume - ent.Comp.MinFootprintVolume) *
            (solution.Comp.Solution.Volume / ent.Comp.MaxFootVolume) + ent.Comp.MinFootprintVolume);

    private static FixedPoint2 GetBodyprintVolume(Entity<FootprintOwnerComponent> ent, Entity<SolutionComponent> solution)
        => FixedPoint2.Min(solution.Comp.Solution.Volume,
            (ent.Comp.MaxBodyprintVolume - ent.Comp.MinBodyprintVolume) *
            (solution.Comp.Solution.Volume / ent.Comp.MaxBodyVolume) + ent.Comp.MinBodyprintVolume);

    private bool TryGetSource(Entity<FootprintOwnerComponent> ent, bool standing,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solution, out bool clothing)
    {
        solution = null;
        clothing = false;
        var hadFootwear = false;
        if (standing && TryGetWornDirt(ent, SlotFlags.FEET, out solution, out hadFootwear))
        {
            clothing = true;
            return true;
        }
        if (standing && hadFootwear)
            return false;

        var hadSocks = false;
        if (standing && TryGetWornDirt(ent, SlotFlags.SOCKS, out solution, out hadSocks))
        {
            clothing = true;
            return true;
        }
        if (standing && hadSocks)
            return false;
        return _solution.TryGetSolution(ent.Owner, FootprintSolution, out solution, out _);
    }

    private bool HasWornItem(EntityUid wearer, SlotFlags slots)
        => _inventory.TryGetContainerSlotEnumerator(wearer, out var enumerator, slots) && enumerator.NextItem(out _);

    private bool TryGetWornDirt(EntityUid wearer, SlotFlags slots,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solution,
        out bool hadItem)
    {
        solution = null;
        hadItem = false;
        if (!_inventory.TryGetContainerSlotEnumerator(wearer, out var enumerator, slots))
            return false;
        while (enumerator.NextItem(out var item))
        {
            hadItem = true;
            if (TryComp<ClothingDirtableComponent>(item, out var dirtable) &&
                _solution.TryGetSolution(item, dirtable.Solution, out var dirtEnt, out var dirt) && dirt.Volume > 0)
            {
                solution = dirtEnt.Value;
                return true;
            }
        }
        return false;
    }

    private bool TryGetAnchoredEntity<T>(Entity<MapGridComponent> grid, Vector2i tile,
        [NotNullWhen(true)] out Entity<T>? entity) where T : IComponent
    {
        var enumerator = _map.GetAnchoredEntitiesEnumerator(grid, grid, tile);
        var query = GetEntityQuery<T>();
        while (enumerator.MoveNext(out var uid))
        {
            if (!query.TryComp(uid, out var component))
                continue;
            entity = (uid.Value, component);
            return true;
        }
        entity = null;
        return false;
    }
}
