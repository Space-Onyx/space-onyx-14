using Content.Client.Atmos; // <Onyx-RPDPipeLayers>
using Content.Client.Gameplay;
using Content.Client.Hands.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.RCD; // <Onyx-RPDPipeLayers>
using Content.Shared.Interaction;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes; // <Onyx-RPDPipeLayers>

namespace Content.Client.RCD;

public sealed partial class AlignRCDConstruction : AlignAtmosPipeLayers // <Onyx-RPDPipeLayers-edited>
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!; // <Onyx-RPDPipeLayers>
    private readonly SharedMapSystem _mapSystem;
    private readonly HandsSystem _handsSystem;
    private readonly RCDSystem _rcdSystem;
    private readonly SharedTransformSystem _transformSystem;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IStateManager _stateManager = default!;

    private const float PlaceColorBaseAlpha = 0.5f;

    /// <summary>
    /// This placement mode is not on the engine because it is content specific (i.e., for the RCD)
    /// </summary>
    public AlignRCDConstruction(PlacementManager pMan) : base(pMan)
    {
        IoCManager.InjectDependencies(this);
        _mapSystem = _entityManager.System<SharedMapSystem>();
        _handsSystem = _entityManager.System<HandsSystem>();
        _rcdSystem = _entityManager.System<RCDSystem>();
        _transformSystem = _entityManager.System<SharedTransformSystem>();

        ValidPlaceColor = ValidPlaceColor.WithAlpha(PlaceColorBaseAlpha);
    }

    public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
    {
        base.AlignPlacementMode(mouseScreen); // <Onyx-RPDPipeLayers-edited>
        if (!UsesPipeLayers()) // <Onyx-RPDPipeLayers>
            return; // <Onyx-RPDPipeLayers>

        var player = _playerManager.LocalSession?.AttachedEntity; // <Onyx-RPDPipeLayers>
        if (player != null && _handsSystem.TryGetActiveItem(player.Value, out var held) && // <Onyx-RPDPipeLayers>
            _entityManager.TryGetComponent<RCDComponent>(held, out var rcd) && rcd.IsRpd) // <Onyx-RPDPipeLayers>
            _rcdSystem.SetConstructionPipeLayer((held.Value, rcd), CurrentPipeLayer); // <Onyx-RPDPipeLayers>
    }

    protected override bool UsesPipeLayers() // <Onyx-RPDPipeLayers>
    {
        if (pManager.CurrentPermission?.MobUid is not { } rcd || // <Onyx-RPDPipeLayers>
            !_entityManager.TryGetComponent<RCDComponent>(rcd, out var component)) // <Onyx-RPDPipeLayers>
            return false; // <Onyx-RPDPipeLayers>

        return _prototypeManager.TryIndex<RCDPrototype>(component.ProtoId, out var prototype) && prototype.PipeLayers; // <Onyx-RPDPipeLayers>
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;

        // If the destination is out of interaction range, set the placer alpha to zero
        if (!_entityManager.TryGetComponent<TransformComponent>(player, out var xform))
            return false;

        if (!_transformSystem.InRange(xform.Coordinates, position, SharedInteractionSystem.InteractionRange))
        {
            InvalidPlaceColor = InvalidPlaceColor.WithAlpha(0);
            return false;
        }

        // Otherwise restore the alpha value
        else
        {
            InvalidPlaceColor = InvalidPlaceColor.WithAlpha(PlaceColorBaseAlpha);
        }

        // Determine if player is carrying an RCD in their active hand
        if (!_handsSystem.TryGetActiveItem(player.Value, out var heldEntity))
            return false;

        if (!_entityManager.TryGetComponent<RCDComponent>(heldEntity, out var rcd))
            return false;

        var gridUid = _transformSystem.GetGrid(position);
        if (!_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var mapGrid))
            return false;
        var tile = _mapSystem.GetTileRef(gridUid.Value, mapGrid, position);
        var posVector = _mapSystem.TileIndicesFor(gridUid.Value, mapGrid, position);

        // Determine if the user is hovering over a target
        var currentState = _stateManager.CurrentState;

        if (currentState is not GameplayStateBase screen)
            return false;

        var target = screen.GetClickedEntity(_transformSystem.ToMapCoordinates(UnalignedMouseCoords)); // <Onyx-RPDPipeLayers-edited>

        // Determine if the RCD operation is valid or not
        if (!_rcdSystem.IsRCDOperationStillValid(heldEntity.Value, rcd, gridUid.Value, mapGrid, tile, posVector, target, player.Value, false))
            return false;

        return true;
    }
}
