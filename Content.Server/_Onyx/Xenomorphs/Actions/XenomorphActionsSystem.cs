using Content.Server.DoAfter;
using Content.Shared._Onyx.Xenomorphs.Actions;
using Content.Shared._Onyx.Xenomorphs.Actions.Events;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Xenomorphs.Actions;

public sealed partial class XenomorphActionsSystem : EntitySystem
{
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private AnchorableSystem _anchorable = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ContainerSystem _containers = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private MapSystem _maps = default!;
    [Dependency] private PlasmaCostActionSystem _plasmaCost = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpawnTileEntityActionEvent>(OnSpawn);
        SubscribeLocalEvent<PlaceTileEntityEvent>(OnPlace);
        SubscribeLocalEvent<PlaceTileEntityDoAfterEvent>(OnPlaceDoAfter);
    }

    private void OnSpawn(SpawnTileEntityActionEvent args)
    {
        if (args.Handled)
            return;

        TryComp<PlasmaCostActionComponent>(args.Action, out var plasmaCost);
        var cost = plasmaCost?.PlasmaCost ?? FixedPoint2.Zero;
        if (!_plasmaCost.HasEnoughPlasma(args.Performer, cost) ||
            !Create(args.Performer, args.Performer.ToCoordinates(), args.TileId, args.Entity,
                args.Audio, args.BlockedCollisionLayer, args.BlockedCollisionMask))
            return;

        _plasmaCost.DeductPlasma(args.Performer, cost);
        args.Handled = true;
    }

    private void OnPlace(PlaceTileEntityEvent args)
    {
        if (args.Handled)
            return;

        TryComp<PlasmaCostActionComponent>(args.Action, out var plasmaCost);
        var cost = plasmaCost?.PlasmaCost ?? FixedPoint2.Zero;
        if (args.Length == 0)
        {
            if (_plasmaCost.HasEnoughPlasma(args.Performer, cost) &&
                Create(args.Performer, args.Target, args.TileId, args.Entity, args.Audio,
                    args.BlockedCollisionLayer, args.BlockedCollisionMask))
            {
                _plasmaCost.DeductPlasma(args.Performer, cost);
                args.Handled = true;
            }
            return;
        }

        if (IsBlocked(args.Target, args.BlockedCollisionLayer, args.BlockedCollisionMask))
            return;

        var ev = new PlaceTileEntityDoAfterEvent
        {
            Target = GetNetCoordinates(args.Target),
            Entity = args.Entity,
            TileId = args.TileId,
            Audio = args.Audio,
            BlockedCollisionLayer = args.BlockedCollisionLayer,
            BlockedCollisionMask = args.BlockedCollisionMask,
            PlasmaCost = cost,
            Action = GetNetEntity(args.Action),
        };
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, args.Length, ev, args.Performer)
        {
            BlockDuplicate = true,
            CancelDuplicate = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            Broadcast = true,
        });
    }

    private void OnPlaceDoAfter(PlaceTileEntityDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_plasmaCost.HasEnoughPlasma(args.User, args.PlasmaCost) ||
            !Create(args.User, GetCoordinates(args.Target), args.TileId, args.Entity, args.Audio,
                args.BlockedCollisionLayer, args.BlockedCollisionMask))
            return;

        _plasmaCost.DeductPlasma(args.User, args.PlasmaCost);
        args.Handled = true;
    }

    private bool Create(EntityUid user, EntityCoordinates coordinates, string? tileId, EntProtoId? entity,
        SoundSpecifier? audio, int collisionLayer, int collisionMask)
    {
        if (_containers.IsEntityOrParentInContainer(user))
            return false;

        if (tileId != null)
        {
            if (_transform.GetGrid(coordinates) is not { } grid || !TryComp(grid, out MapGridComponent? mapGrid))
                return false;
            _maps.SetTile(grid, mapGrid, coordinates, new Tile(_tileDefinitions[tileId].TileId));
        }

        _audio.PlayPvs(audio, coordinates);
        if (entity == null || IsBlocked(coordinates, collisionLayer, collisionMask))
            return false;

        Spawn(entity, coordinates);
        return true;
    }

    private bool IsBlocked(EntityCoordinates coordinates, int collisionLayer, int collisionMask)
    {
        if (_transform.GetGrid(coordinates) is not { } grid || !TryComp(grid, out MapGridComponent? mapGrid))
            return true;
        return !_anchorable.TileFree(mapGrid, _maps.TileIndicesFor(grid, mapGrid, coordinates), collisionLayer, collisionMask);
    }
}
