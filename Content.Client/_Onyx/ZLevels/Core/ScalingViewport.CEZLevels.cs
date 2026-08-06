/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using System.Numerics;
using Content.Client._Onyx.ZLevels.Core;
using Content.Shared._Onyx.ZLevels.Apertures.Components;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Content.Shared._Onyx.ZLevels.Core.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private IOverlayManager _overlayManager = default!; // Onyx: multiz
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IGameTiming _timing = default!;

    private CEClientZLevelsSystem? _zLevels;
    private SharedMapSystem? _mapSystem;
    private SharedMapSystem MapSystem => _mapSystem ??= _entityManager.System<SharedMapSystem>();
    // Lazily resolved for linked-deck eye reprojection.
    private SharedTransformSystem? _transform;

    private EntityQuery<TransformComponent>? _xformQuery;
    private EntityQuery<MapComponent>? _mapQuery;
    private IEye? _fallbackEye;
    // Last linked grid the eye stood on. Reused to keep decks aligned while the eye
    // hovers over gridless open space, instead of snapping back to raw world coords.
    private EntityUid? _lastLinkedGrid;
    private readonly Dictionary<int, IRenderTexture> _zApertureTargets = new();
    private readonly HashSet<int> _zApertureValidTargets = new();
    private readonly List<ZLevelAperture> _zApertures = new();
    private readonly HashSet<int> _zApertureRequiredSourceDepths = new();
    private readonly List<int> _zApertureTargetsToRemove = new();
    private readonly Dictionary<int, ZEye> _zEyes = new();
    private readonly List<ZRenderPass> _zRenderPasses = new(CESharedZLevelsSystem.MaxZLevelsBelowRendering + 2);
    private readonly Dictionary<EntityUid, ZRenderPass> _zRenderPassByMap = new();
    private readonly DrawVertexUV2D[] _zApertureVertices = new DrawVertexUV2D[6];
    private readonly ZEye _zAperturePrepareEye = new(0);
    private readonly ZEye _zVisibilityEye = new(0);
    private readonly List<Entity<MapGridComponent>> _zVisibilityGrids = new();
    private readonly List<Box2> _zVisibilityOpenings = new();
    private readonly List<Box2> _zVisibilityChain = new();
    private readonly List<Box2> _zVisibilityNextChain = new();
    private readonly Dictionary<int, CEClientZLevelsSystem.ZRenderVisibility> _zPublishedVisibility = new();
    private ZLevelApertureOverlay? _zApertureOverlay;
    private bool _zApertureCaptureThisFrame;
    private int? _zCurrentRenderDepth;
    private TimeSpan _zLowerRenderGraceUntil;
    private int _zLowerRenderGraceLowestDepth;

    public EntityUid? CEZLevelViewEntity { get; set; }

    /// <summary>
    /// We are looking for at least one empty tile on the screen.
    /// This is used to ensure that it makes sense to draw the z-planes and that they are visible.
    /// </summary>
    public bool TryFindEmptyTiles(EntityUid mapUid)
    {
        if (_xformQuery is null || !_xformQuery.Value.TryComp(mapUid, out var xform))
            return true;

        var drawBox = GetDrawBox();
        var mapId = xform.MapID;

        var corners = new[]
        {
            _eyeManager.ScreenToMap(drawBox.BottomLeft).Position,
            _eyeManager.ScreenToMap(drawBox.BottomRight).Position,
            _eyeManager.ScreenToMap(drawBox.TopLeft).Position,
            _eyeManager.ScreenToMap(drawBox.TopRight).Position
        };

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var c in corners)
        {
            if (c.X < minX)
                minX = c.X;
            if (c.Y < minY)
                minY = c.Y;
            if (c.X > maxX)
                maxX = c.X;
            if (c.Y > maxY)
                maxY = c.Y;
        }

        var mapCoordsBottomLeft = new MapCoordinates(new Vector2(minX, minY), mapId);
        var mapCoordsTopRight = new MapCoordinates(new Vector2(maxX, maxY), mapId);

        if (!MapSystem.TryFindGridAt(mapUid, mapCoordsBottomLeft.Position, out _, out var grid))
            return true;

        var gridEnt = new Entity<MapGridComponent>(grid.Owner, grid);
        var tileBottomLeft = MapSystem.TileIndicesFor(gridEnt, mapCoordsBottomLeft);
        var tileTopRight = MapSystem.TileIndicesFor(gridEnt, mapCoordsTopRight);

        for (var x = tileBottomLeft.X - 1; x <= tileTopRight.X + 1; x++)
        {
            for (var y = tileBottomLeft.Y - 1; y <= tileTopRight.Y + 1; y++)
            {
                var tile = MapSystem.GetTileRef(gridEnt, new Vector2i(x, y));
                var tileDef = (ContentTileDefinition)_tile[tile.Tile.TypeId];
                if (tileDef.ZTransparent || tile.Tile.IsEmpty)
                    return true;
            }
        }

        return false;
    }

    private bool TryResolveZMapEntity(EntityUid playerMapUid, EntityUid? playerGridUid, int depthOffset, out EntityUid mapUid, out MapId mapId, out EntityUid? peerGridUid) // Onyx: multiz
    {
        mapUid = default;
        mapId = default;
        peerGridUid = null; // Onyx: multiz

        // Prefer the linked structure the player is on.
        if (playerGridUid != null &&
            _entityManager.TryGetComponent<CEZLinkedGridComponent>(playerGridUid.Value, out var linked))
        {
            var targetDepth = linked.Depth + depthOffset;

            if (linked.PeerGrids.TryGetValue(targetDepth, out var peerGrid))
            {
                if (_xformQuery!.Value.TryComp(peerGrid, out var peerXform) &&
                    peerXform.MapUid != null &&
                    _mapQuery!.Value.TryComp(peerXform.MapUid.Value, out var peerMapComp))
                {
                    mapUid = peerXform.MapUid.Value;
                    mapId = peerMapComp.MapId;
                    peerGridUid = peerGrid; // Onyx: multiz
                    return true;
                }
            }

            // Linked grids should not fall back to unrelated map-level decks.
            return false;
        }

        // Fallback for players not on a linked grid.
        if (_zLevels!.TryMapOffset(playerMapUid, depthOffset, out var targetMap))
        {
            if (_mapQuery!.Value.TryComp(targetMap.Value, out var mapComp))
            {
                mapUid = targetMap.Value;
                mapId = mapComp.MapId;
                return true;
            }
        }

        return false;
    }

    private Vector2 GetRawEyePosition(TransformComponent playerXform)
    {
        _transform ??= _entityManager.System<SharedTransformSystem>();
        return _fallbackEye?.Position.Position ?? _eye?.Position.Position ?? _transform.GetWorldPosition(playerXform);
    }

    /// <summary>
    /// Resolves the linked grid whose frame the eye should be reprojected through.
    /// On a grid we use it directly; over gridless open space we fall back to the linked
    /// grid under the eye, then to the last linked grid we stood on. This keeps the decks
    /// below aligned instead of snapping to raw world coordinates the moment the eye
    /// leaves the grid (the two linked decks sit at different world offsets, and only the
    /// grid-frame reprojection compensates for that).
    /// </summary>
    private EntityUid? ResolveEffectiveGrid(TransformComponent playerXform)
    {
        // On a grid: use it directly and remember it for later gridless frames.
        if (playerXform.GridUid is { } gridUid)
        {
            if (_entityManager.HasComponent<CEZLinkedGridComponent>(gridUid))
                _lastLinkedGrid = gridUid;

            return gridUid;
        }

        // Gridless: reproject through a linked grid directly under the eye, if any.
        if (playerXform.MapUid is { } mapUid)
        {
            var eyeWorld = GetRawEyePosition(playerXform);
            if (MapSystem.TryFindGridAt(mapUid, eyeWorld, out var foundGridUid, out _) &&
                _entityManager.HasComponent<CEZLinkedGridComponent>(foundGridUid))
            {
                _lastLinkedGrid = foundGridUid;
                return foundGridUid;
            }
        }

        // Over open space: reuse the last linked grid we stood on so the transition off
        // the edge stays continuous, as long as it still exists on the same map.
        if (_lastLinkedGrid is { } cached &&
            _entityManager.EntityExists(cached) &&
            _xformQuery!.Value.TryComp(cached, out var cachedXform) &&
            cachedXform.MapUid == playerXform.MapUid &&
            _entityManager.HasComponent<CEZLinkedGridComponent>(cached))
        {
            return cached;
        }

        _lastLinkedGrid = null;
        return null;
    }

    // Reproject the eye into the peer grid's world space.
    private Vector2? GetEyeGridPosition(Vector2 rawEyePosition, EntityUid? currentGridUid)
    {
        _transform ??= _entityManager.System<SharedTransformSystem>();
        if (currentGridUid is not { } currentGrid ||
            !Matrix3x2.Invert(_transform.GetWorldMatrix(currentGrid), out var inverseCurrentGrid))
            return null;

        return Vector2.Transform(rawEyePosition, inverseCurrentGrid);
    }

    private MapCoordinates GetResolvedEyePosition(
        Vector2 rawEyePosition,
        Vector2? eyeGridPosition,
        EntityUid? peerGridUid,
        MapId targetMapId)
    {
        if (peerGridUid is not { } peerGrid || eyeGridPosition is not { } gridPosition)
            return new MapCoordinates(rawEyePosition, targetMapId);

        _transform ??= _entityManager.System<SharedTransformSystem>();
        return new MapCoordinates(Vector2.Transform(gridPosition, _transform.GetWorldMatrix(peerGrid)), targetMapId);
    }

    private void RenderZLevels(IClydeViewport viewport, DrawingHandleScreen screenHandle)
    {
        var isMainViewport = ReferenceEquals(_eyeManager.MainViewport, this);
        if (isMainViewport)
            _entityManager.System<CEClientZLevelsSystem>().ClearRenderVisibility();

        if (_eye is null)
        {
            viewport.Render();
            return;
        }

        _fallbackEye = _eye;

        _xformQuery ??= _entityManager.GetEntityQuery<TransformComponent>();
        _mapQuery ??= _entityManager.GetEntityQuery<MapComponent>();
        _zLevels ??= _entityManager.System<CEClientZLevelsSystem>();
        _mapSystem ??= _entityManager.System<SharedMapSystem>();

        if (_player.LocalEntity is null)
        {
            viewport.Render();
            return;
        }

        if (!_entityManager.TryGetComponent<CEZLevelViewerComponent>(_player.LocalEntity.Value, out var zLevelViewer))
        {
            viewport.Render();
            return;
        }

        // Remote eyes render z-levels from the eye's grid/map, not the viewer body's.
        var relayTarget = GetRelayViewTarget();
        var viewEntity = CEZLevelViewEntity ?? relayTarget ?? _player.LocalEntity.Value;
        if (!_xformQuery.Value.TryComp(viewEntity, out var playerXform))
        {
            viewEntity = _player.LocalEntity.Value;
            if (!_xformQuery.Value.TryComp(viewEntity, out playerXform))
            {
                viewport.Render();
                return;
            }
        }

        if (playerXform.MapUid is null)
        {
            viewport.Render();
            return;
        }

        // Grid frame to reproject the eye/decks through, tolerant of gridless open space.
        var effectiveGridUid = ResolveEffectiveGrid(playerXform);

        var visibleBelow = Math.Clamp(
            _cfg.GetCVar(CCVars.CEZLevelsVisibleBelow),
            0,
            CESharedZLevelsSystem.MaxZLevelsBelowRendering);
        var rawEyePosition = GetRawEyePosition(playerXform);
        var eyeGridPosition = GetEyeGridPosition(rawEyePosition, effectiveGridUid);
        var rotation = -_fallbackEye.Rotation;

        _zRenderPasses.Clear();
        _zRenderPassByMap.Clear();
        ClearPublishedVisibility();
        for (var depth = -visibleBelow; depth <= (zLevelViewer.LookUp ? 1 : 0); depth++)
        {
            EntityUid mapUid;
            MapId mapId;
            EntityUid? peerGridUid;
            if (depth == 0)
            {
                mapUid = playerXform.MapUid.Value;
                if (!_mapQuery.Value.TryComp(mapUid, out var mapComp))
                    continue;
                mapId = mapComp.MapId;
                peerGridUid = effectiveGridUid;
            }
            else if (!TryResolveZMapEntity(playerXform.MapUid.Value, effectiveGridUid, depth, out mapUid, out mapId, out peerGridUid))
            {
                continue;
            }

            var eyePosition = depth == 0
                ? _fallbackEye.Position
                : GetResolvedEyePosition(rawEyePosition, eyeGridPosition, peerGridUid, mapId);
            var offset = depth == 0
                ? _fallbackEye.Offset
                : _fallbackEye.Offset + rotation.ToWorldVec() * CEClientZLevelsSystem.ZLevelOffset * depth;
            var pass = new ZRenderPass(depth, mapUid, eyePosition, _fallbackEye.DrawFov && depth >= 0, offset);
            _zRenderPasses.Add(pass);
            _zRenderPassByMap[mapUid] = pass;
        }

        CullInvisibleLowerRenderPasses(viewport, visibleBelow);
        PublishMainViewportVisibility(viewport);

        if (_zRenderPasses.Count == 0)
        {
            viewport.Render();
            return;
        }

        var lowestDepth = _zRenderPasses[0].Depth;
        var highestDepth = _zRenderPasses[^1].Depth;
        _zApertureValidTargets.Clear();
        PrepareZLevelApertures(lowestDepth, viewport);
        RemoveUnusedZLevelApertureTargets();

        if (_zApertureCaptureThisFrame)
        {
            EnsureZLevelApertureTargets(viewport.RenderTarget.Size);
            EnsureZLevelApertureOverlay();
        }

        var placementOverlay = _overlayManager.AllOverlays
            .FirstOrDefault(o => o.GetType().FullName == "Robust.Client.Placement.PlacementManager+PlacementOverlay");
        var parallaxOverlay = _overlayManager.AllOverlays
            .OfType<Content.Client.Parallax.ParallaxOverlay>()
            .FirstOrDefault();
        var playerEye = _fallbackEye as Robust.Shared.Graphics.Eye;
        var playerEyePosition = playerEye?.Position ?? default;
        var playerEyeDrawFov = playerEye?.DrawFov ?? default;
        var playerEyeDrawLight = playerEye?.DrawLight ?? default;
        var playerEyeOffset = playerEye?.Offset ?? default;
        var playerEyeRotation = playerEye?.Rotation ?? default;
        var playerEyeScale = playerEye?.Scale ?? default;
        var originalClearColor = viewport.ClearColor;
        var placementRemoved = false;

        try
        {
            foreach (var pass in _zRenderPasses)
            {
                IEye eye;
                if (pass.Depth == highestDepth && playerEye != null)
                {
                    // Player-only overlays compare eye identity. Use the real eye only on the final
                    // compositing pass so flash, blindness and vision shaders affect the whole frame once.
                    playerEye.Position = pass.EyePosition;
                    playerEye.DrawFov = pass.DrawFov;
                    playerEye.DrawLight = _fallbackEye.DrawLight;
                    playerEye.Offset = pass.Offset;
                    playerEye.Rotation = _fallbackEye.Rotation;
                    playerEye.Scale = _fallbackEye.Scale;
                    eye = playerEye;
                }
                else
                {
                    if (!_zEyes.TryGetValue(pass.Depth, out var zEye))
                        _zEyes[pass.Depth] = zEye = new ZEye(pass.Depth);

                    zEye.DrawParallax = pass.Depth == lowestDepth;
                    zEye.Position = pass.EyePosition;
                    zEye.DrawFov = pass.DrawFov;
                    zEye.DrawLight = _fallbackEye.DrawLight;
                    zEye.Offset = pass.Offset;
                    zEye.Rotation = _fallbackEye.Rotation;
                    zEye.Scale = _fallbackEye.Scale;
                    eye = zEye;
                }

                _zCurrentRenderDepth = pass.Depth;
                if (parallaxOverlay != null)
                    parallaxOverlay.ZPassDrawEnabled = pass.Depth == lowestDepth;
                viewport.Eye = eye;
                viewport.ClearColor = pass.Depth == lowestDepth ? Color.Black : null;

                #region Onyx: multiz
                // Hide duplicate previews, plus unsafe mid-z-move placement snaps.
                var hidePlacement = placementOverlay != null
                    && (pass.Depth != 0 || PlacementOverlayWouldDesync(pass.MapUid));
                if (hidePlacement && !placementRemoved)
                    placementRemoved = _overlayManager.RemoveOverlay(placementOverlay!);
                else if (!hidePlacement && placementRemoved)
                {
                    _overlayManager.AddOverlay(placementOverlay!);
                    placementRemoved = false;
                }

                viewport.Render();

                if (_zApertureRequiredSourceDepths.Contains(pass.Depth))
                    CaptureZLevelApertureTexture(screenHandle, viewport, pass.Depth);
                #endregion Onyx: multiz
            }
        }
        finally
        {
            _zCurrentRenderDepth = null;
            if (parallaxOverlay != null)
                parallaxOverlay.ZPassDrawEnabled = true;

            if (placementRemoved)
                _overlayManager.AddOverlay(placementOverlay!);

            if (playerEye != null)
            {
                playerEye.Position = playerEyePosition;
                playerEye.DrawFov = playerEyeDrawFov;
                playerEye.DrawLight = playerEyeDrawLight;
                playerEye.Offset = playerEyeOffset;
                playerEye.Rotation = playerEyeRotation;
                playerEye.Scale = playerEyeScale;
            }

            Eye = _fallbackEye;
            viewport.Eye = Eye;
            viewport.ClearColor = originalClearColor;
        }
    }

    private void CullInvisibleLowerRenderPasses(IClydeViewport viewport, int visibleBelow)
    {
        if (visibleBelow <= 0 || !_zRenderPasses.Any(pass => pass.Depth == 0))
        {
            _zLowerRenderGraceLowestDepth = 0;
            _zLowerRenderGraceUntil = TimeSpan.Zero;
            return;
        }

        var maxOpeningRects = Math.Max(0, _cfg.GetCVar(CCVars.CEZLevelsMaxOpeningRectsPerPass));
        var openingLimit = maxOpeningRects is 0 or int.MaxValue ? int.MaxValue : maxOpeningRects + 1;
        _zVisibilityChain.Clear();
        var lowestVisibleDepth = 0;

        for (var depth = 0; depth > -visibleBelow; depth--)
        {
            if (!_zRenderPasses.Any(pass => pass.Depth == depth) ||
                !_zRenderPasses.Any(pass => pass.Depth == depth - 1))
            {
                // Missing client state cannot prove that deeper passes are occluded.
                lowestVisibleDepth = _zRenderPasses.Where(pass => pass.Depth < 0).Select(pass => pass.Depth).DefaultIfEmpty(0).Min();
                break;
            }

            var pass = _zRenderPasses.First(candidate => candidate.Depth == depth);
            ConfigureVisibilityEye(pass);
            var fullWorldBounds = GetViewportWorldBounds(viewport, _zVisibilityEye);
            _zVisibilityOpenings.Clear();
            var foundOpening = _zLevels!.TryFindOpeningBounds(
                pass.EyePosition.MapId,
                fullWorldBounds,
                _zVisibilityOpenings,
                openingLimit,
                _zVisibilityGrids);

            AppendVisibleApertureBounds(pass, viewport, _zVisibilityOpenings);
            foundOpening |= _zVisibilityOpenings.Count > 0;

            // Multiple grids or off-grid viewport corners are not proof of opaque coverage.
            if (!IsCoveredBySingleGrid(pass.MapUid, fullWorldBounds))
            {
                _zVisibilityOpenings.Clear();
                _zVisibilityOpenings.Add(fullWorldBounds);
                foundOpening = true;
            }

            if (!foundOpening)
                break;

            // Missing concrete bounds or overflow cannot prove occlusion.
            if (_zVisibilityOpenings.Count == 0 ||
                maxOpeningRects > 0 && _zVisibilityOpenings.Count > maxOpeningRects)
            {
                lowestVisibleDepth = depth - 1;
                _zVisibilityChain.Clear();
                _zVisibilityChain.Add(new Box2(Vector2.Zero, viewport.Size));
                continue;
            }

            _zVisibilityNextChain.Clear();
            if ((long) _zVisibilityOpenings.Count * _zVisibilityChain.Count > openingLimit)
            {
                _zVisibilityNextChain.Add(new Box2(Vector2.Zero, viewport.Size));
            }
            else
            {
                foreach (var opening in _zVisibilityOpenings)
                {
                    var screenBounds = WorldBoundsToViewport(opening, _zVisibilityEye, viewport);
                    if (depth == 0)
                    {
                        _zVisibilityNextChain.Add(screenBounds);
                        continue;
                    }

                    foreach (var previous in _zVisibilityChain)
                    {
                        if (!TryIntersectBounds(screenBounds, previous, out var intersection))
                            continue;

                        _zVisibilityNextChain.Add(intersection);
                        if (maxOpeningRects > 0 && _zVisibilityNextChain.Count > maxOpeningRects)
                        {
                            _zVisibilityNextChain.Clear();
                            _zVisibilityNextChain.Add(new Box2(Vector2.Zero, viewport.Size));
                            break;
                        }
                    }
                }
            }

            if (_zVisibilityNextChain.Count == 0)
                break;

            _zVisibilityChain.Clear();
            _zVisibilityChain.AddRange(_zVisibilityNextChain);
            lowestVisibleDepth = depth - 1;
            var published = GetPublishedVisibility(lowestVisibleDepth);
            published.MapId = _zRenderPasses.First(candidate => candidate.Depth == lowestVisibleDepth).EyePosition.MapId;
            published.Regions.Clear();
            published.Regions.AddRange(_zVisibilityChain);
        }

        var graceSeconds = Math.Max(0f, _cfg.GetCVar(CCVars.CEZLevelsLowerRenderVisibilityGrace));
        if (lowestVisibleDepth < _zLowerRenderGraceLowestDepth ||
            lowestVisibleDepth == _zLowerRenderGraceLowestDepth ||
            _timing.CurTime > _zLowerRenderGraceUntil)
        {
            _zLowerRenderGraceLowestDepth = lowestVisibleDepth;
            _zLowerRenderGraceUntil = lowestVisibleDepth < 0
                ? _timing.CurTime + TimeSpan.FromSeconds(graceSeconds)
                : TimeSpan.Zero;
        }
        else if (graceSeconds > 0f)
        {
            lowestVisibleDepth = Math.Max(_zLowerRenderGraceLowestDepth, -visibleBelow);
        }

        _zRenderPasses.RemoveAll(pass => pass.Depth < lowestVisibleDepth);
        _zRenderPassByMap.Clear();
        foreach (var pass in _zRenderPasses)
            _zRenderPassByMap[pass.MapUid] = pass;
    }

    private void ClearPublishedVisibility()
    {
        foreach (var visibility in _zPublishedVisibility.Values)
            visibility.Regions.Clear();
    }

    private CEClientZLevelsSystem.ZRenderVisibility GetPublishedVisibility(int depth)
    {
        if (_zPublishedVisibility.TryGetValue(depth, out var visibility))
            return visibility;

        visibility = new CEClientZLevelsSystem.ZRenderVisibility();
        _zPublishedVisibility[depth] = visibility;
        return visibility;
    }

    private void PublishMainViewportVisibility(IClydeViewport viewport)
    {
        if (!ReferenceEquals(_eyeManager.MainViewport, this))
            return;

        foreach (var pass in _zRenderPasses)
        {
            if (pass.Depth >= 0 || !_zPublishedVisibility.TryGetValue(pass.Depth, out var visibility) || visibility.Regions.Count == 0)
                continue;

            visibility.MapId = pass.EyePosition.MapId;
            visibility.EyePosition = pass.EyePosition;
            visibility.EyeOffset = pass.Offset;
            visibility.EyeRotation = _fallbackEye!.Rotation;
            visibility.EyeScale = _fallbackEye.Scale;
            visibility.ViewportSize = viewport.Size;
            visibility.RenderScale = viewport.RenderScale;
        }

        _zLevels!.PublishRenderVisibility(_zPublishedVisibility, _fallbackEye!);
    }

    private bool IsCoveredBySingleGrid(EntityUid mapUid, Box2 worldBounds)
    {
        EntityUid? coveringGrid = null;
        return IsCoveredByGrid(mapUid, worldBounds.BottomLeft, ref coveringGrid) &&
               IsCoveredByGrid(mapUid, worldBounds.BottomRight, ref coveringGrid) &&
               IsCoveredByGrid(mapUid, worldBounds.TopLeft, ref coveringGrid) &&
               IsCoveredByGrid(mapUid, worldBounds.TopRight, ref coveringGrid);
    }

    private bool IsCoveredByGrid(EntityUid mapUid, Vector2 point, ref EntityUid? coveringGrid)
    {
        if (!MapSystem.TryFindGridAt(mapUid, point, out var gridUid, out _) ||
            coveringGrid != null && coveringGrid != gridUid)
            return false;

        coveringGrid = gridUid;
        return true;
    }

    private void AppendVisibleApertureBounds(ZRenderPass pass, IClydeViewport viewport, List<Box2> bounds)
    {
        var query = _entityManager.EntityQueryEnumerator<CEZLevelApertureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aperture, out var xform))
        {
            if (aperture.TargetDepth != -1 || xform.MapUid != pass.MapUid ||
                aperture.SpritePixelSize <= 0 || aperture.PixelSize.X <= 0 || aperture.PixelSize.Y <= 0)
                continue;

            var matrix = GetApertureDrawMatrix(uid, xform, _zVisibilityEye);
            var localQuad = GetPixelLocalQuad(aperture.PixelOffset, aperture.PixelSize, aperture.SpritePixelSize);
            var worldQuad = TransformQuad(localQuad, matrix);
            var viewportQuad = WorldToViewport(worldQuad, _zVisibilityEye, viewport);
            if (viewportQuad.Bounds.Intersects(new UIBox2(Vector2.Zero, viewport.Size)))
                bounds.Add(GetQuadBounds(worldQuad));
        }
    }

    private static Box2 GetQuadBounds(ApertureQuad quad)
    {
        var min = Vector2.Min(Vector2.Min(quad.TopLeft, quad.TopRight), Vector2.Min(quad.BottomLeft, quad.BottomRight));
        var max = Vector2.Max(Vector2.Max(quad.TopLeft, quad.TopRight), Vector2.Max(quad.BottomLeft, quad.BottomRight));
        return new Box2(min, max);
    }

    private void ConfigureVisibilityEye(ZRenderPass pass)
    {
        _zVisibilityEye.Position = pass.EyePosition;
        _zVisibilityEye.Offset = pass.Offset;
        _zVisibilityEye.Rotation = _fallbackEye!.Rotation;
        _zVisibilityEye.Scale = _fallbackEye.Scale;
    }

    private static Box2 GetViewportWorldBounds(IClydeViewport viewport, IEye eye)
    {
        var size = (Vector2) viewport.Size;
        var topLeft = viewport.RenderTarget.LocalToWorld(eye, Vector2.Zero, viewport.RenderScale);
        var topRight = viewport.RenderTarget.LocalToWorld(eye, new Vector2(size.X, 0f), viewport.RenderScale);
        var bottomLeft = viewport.RenderTarget.LocalToWorld(eye, new Vector2(0f, size.Y), viewport.RenderScale);
        var bottomRight = viewport.RenderTarget.LocalToWorld(eye, size, viewport.RenderScale);
        var min = Vector2.Min(Vector2.Min(topLeft, topRight), Vector2.Min(bottomLeft, bottomRight));
        var max = Vector2.Max(Vector2.Max(topLeft, topRight), Vector2.Max(bottomLeft, bottomRight));
        return new Box2(min, max);
    }

    private static Box2 WorldBoundsToViewport(Box2 worldBounds, IEye eye, IClydeViewport viewport)
    {
        var matrix = viewport.RenderTarget.GetWorldToLocalMatrix(eye, viewport.RenderScale);
        return matrix.TransformBox(worldBounds);
    }

    private static bool TryIntersectBounds(Box2 left, Box2 right, out Box2 intersection)
    {
        var min = Vector2.Max(left.BottomLeft, right.BottomLeft);
        var max = Vector2.Min(left.TopRight, right.TopRight);
        if (min.X >= max.X || min.Y >= max.Y)
        {
            intersection = default;
            return false;
        }

        intersection = new Box2(min, max);
        return true;
    }

    // Returns the remote eye currently viewed by the local player, if any.
    private EntityUid? GetRelayViewTarget()
    {
        if (_player.LocalEntity is not { } local)
            return null;

        return _entityManager.TryGetComponent<EyeComponent>(local, out var eye) ? eye.Target : null;
    }

    // True when an active placement preview targets a map other than the one being rendered.
    private bool PlacementOverlayWouldDesync(EntityUid renderedMapUid)
    {
        if (!_placement.IsActive || _placement.CurrentMode is not { } mode)
            return false;
        if (_xformQuery is not { } query || !query.TryComp(mode.MouseCoords.EntityId, out var coordsXform))
            return false;
        return coordsXform.MapUid != renderedMapUid;
    }

    private void PrepareZLevelApertures(int lowestDepth, IClydeViewport viewport)
    {
        _zApertureRequiredSourceDepths.Clear();
        _zApertures.Clear();
        _transform ??= _entityManager.System<SharedTransformSystem>();
        var viewportBounds = UIBox2.FromDimensions(Vector2.Zero, viewport.Size);

        var query = _entityManager.EntityQueryEnumerator<CEZLevelApertureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aperture, out var xform))
        {
            if (aperture.TargetDepth != -1 || xform.MapUid is not { } mapUid ||
                !_zRenderPassByMap.TryGetValue(mapUid, out var pass) ||
                pass.Depth <= lowestDepth ||
                aperture.SpritePixelSize <= 0 ||
                aperture.PixelSize.X <= 0 ||
                aperture.PixelSize.Y <= 0)
                continue;

            var sourceDepth = pass.Depth - 1;
            if (sourceDepth < lowestDepth)
                continue;

            _zAperturePrepareEye.Position = pass.EyePosition;
            _zAperturePrepareEye.Offset = pass.Offset;
            _zAperturePrepareEye.Rotation = _fallbackEye!.Rotation;
            _zAperturePrepareEye.Scale = _fallbackEye.Scale;

            var matrix = GetApertureDrawMatrix(uid, xform, _zAperturePrepareEye);
            var localQuad = GetPixelLocalQuad(aperture.PixelOffset, aperture.PixelSize, aperture.SpritePixelSize);
            var worldQuad = TransformQuad(localQuad, matrix);
            var viewportQuad = WorldToViewport(worldQuad, _zAperturePrepareEye, viewport);
            if (!viewportQuad.Bounds.Intersects(viewportBounds))
                continue;

            _zApertureRequiredSourceDepths.Add(sourceDepth);
            _zApertures.Add(new ZLevelAperture(pass.Depth, sourceDepth, worldQuad, viewportQuad));
        }

        _zApertureCaptureThisFrame = _zApertureRequiredSourceDepths.Count != 0;
    }

    private void EnsureZLevelApertureTargets(Vector2i size)
    {
        foreach (var depth in _zApertureRequiredSourceDepths)
        {
            if (_zApertureTargets.TryGetValue(depth, out var existing) && existing.Size == size)
                continue;

            var replacement = _clyde.CreateRenderTarget(
                size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                new TextureSampleParameters { Filter = false },
                $"z-level-aperture-depth-{depth}");
            _zApertureTargets[depth] = replacement;
            existing?.Dispose();
        }
    }

    private void CaptureZLevelApertureTexture(DrawingHandleScreen screenHandle, IClydeViewport viewport, int depth)
    {
        if (!_zApertureTargets.TryGetValue(depth, out var target))
            return;

        var targetBox = UIBox2.FromDimensions(Vector2.Zero, viewport.RenderTarget.Size);
        screenHandle.RenderInRenderTarget(target, () =>
        {
            screenHandle.DrawTextureRect(viewport.RenderTarget.Texture, targetBox);
        }, Color.Transparent);

        _zApertureValidTargets.Add(depth);
    }

    private void DrawZLevelAperturesWorld(DrawingHandleWorld worldHandle, IClydeViewport viewport)
    {
        if (!_zApertureCaptureThisFrame ||
            _fallbackEye is null ||
            _viewport is null ||
            _zCurrentRenderDepth is not { } currentDepth ||
            !ReferenceEquals(viewport, _viewport))
        {
            return;
        }

        foreach (var entry in _zApertures)
        {
            if (entry.Depth != currentDepth)
                continue;

            if (!_zApertureValidTargets.Contains(entry.SourceDepth) ||
                !_zApertureTargets.TryGetValue(entry.SourceDepth, out var sourceTarget))
            {
                continue;
            }

            // Same-screen-position sampling works because z-level grids are fixed relative to each other.
            DrawZLevelApertureQuad(worldHandle, sourceTarget.Texture, sourceTarget.Size, entry.WorldQuad, entry.ViewportQuad);
        }
    }

    private void RemoveUnusedZLevelApertureTargets()
    {
        _zApertureTargetsToRemove.Clear();
        foreach (var depth in _zApertureTargets.Keys)
        {
            if (!_zApertureRequiredSourceDepths.Contains(depth))
                _zApertureTargetsToRemove.Add(depth);
        }

        foreach (var depth in _zApertureTargetsToRemove)
        {
            _zApertureTargets[depth].Dispose();
            _zApertureTargets.Remove(depth);
        }
    }

    private void DrawZLevelApertureQuad(
        DrawingHandleWorld worldHandle,
        Texture texture,
        Vector2i textureSize,
        ApertureQuad destinationWorldQuad,
        ApertureQuad sourceViewportQuad)
    {
        var vertices = _zApertureVertices;

        vertices[0] = new DrawVertexUV2D(destinationWorldQuad.TopLeft, ViewportPointToTextureUv(sourceViewportQuad.TopLeft, textureSize));
        vertices[1] = new DrawVertexUV2D(destinationWorldQuad.TopRight, ViewportPointToTextureUv(sourceViewportQuad.TopRight, textureSize));
        vertices[2] = new DrawVertexUV2D(destinationWorldQuad.BottomLeft, ViewportPointToTextureUv(sourceViewportQuad.BottomLeft, textureSize));
        vertices[3] = new DrawVertexUV2D(destinationWorldQuad.TopRight, ViewportPointToTextureUv(sourceViewportQuad.TopRight, textureSize));
        vertices[4] = new DrawVertexUV2D(destinationWorldQuad.BottomRight, ViewportPointToTextureUv(sourceViewportQuad.BottomRight, textureSize));
        vertices[5] = new DrawVertexUV2D(destinationWorldQuad.BottomLeft, ViewportPointToTextureUv(sourceViewportQuad.BottomLeft, textureSize));

        worldHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, texture, vertices);
    }

    private static Vector2 ViewportPointToTextureUv(Vector2 point, Vector2i textureSize)
    {
        return new Vector2(
            point.X / textureSize.X,
            1f - point.Y / textureSize.Y);
    }

    private static ApertureQuad WorldToViewport(ApertureQuad worldQuad, IEye eye, IClydeViewport viewport)
    {
        return new ApertureQuad(
            viewport.RenderTarget.WorldToLocal(worldQuad.TopLeft, eye, viewport.RenderScale),
            viewport.RenderTarget.WorldToLocal(worldQuad.TopRight, eye, viewport.RenderScale),
            viewport.RenderTarget.WorldToLocal(worldQuad.BottomLeft, eye, viewport.RenderScale),
            viewport.RenderTarget.WorldToLocal(worldQuad.BottomRight, eye, viewport.RenderScale));
    }

    private static ApertureQuad TransformQuad(ApertureQuad localQuad, Matrix3x2 matrix)
    {
        return new ApertureQuad(
            Vector2.Transform(localQuad.TopLeft, matrix),
            Vector2.Transform(localQuad.TopRight, matrix),
            Vector2.Transform(localQuad.BottomLeft, matrix),
            Vector2.Transform(localQuad.BottomRight, matrix));
    }

    private Matrix3x2 GetApertureDrawMatrix(EntityUid uid, TransformComponent xform, IEye eye)
    {
        if (!_entityManager.TryGetComponent<SpriteComponent>(uid, out var sprite))
            return _transform!.GetWorldMatrix(xform);

        var (worldPosition, worldRotation) = _transform!.GetWorldPositionRotation(xform);
        var angle = (worldRotation + eye.Rotation).Reduced().FlipPositive();
        var cardinal = Angle.Zero;

        if (sprite is { NoRotation: false, SnapCardinals: true })
            cardinal = angle.RoundToCardinalAngle();

        var entityMatrix = Matrix3Helpers.CreateTransform(worldPosition, sprite.NoRotation ? -eye.Rotation : worldRotation - cardinal);
        return Matrix3x2.Multiply(sprite.LocalMatrix, entityMatrix);
    }

    private static ApertureQuad GetPixelLocalQuad(Vector2i pixelOffset, Vector2i pixelSize, int spritePixelSize)
    {
        var spriteSize = spritePixelSize;
        var half = spriteSize / 2f;
        var left = (pixelOffset.X - half) / spriteSize;
        var right = (pixelOffset.X + pixelSize.X - half) / spriteSize;
        var top = (half - pixelOffset.Y) / spriteSize;
        var bottom = (half - pixelOffset.Y - pixelSize.Y) / spriteSize;

        return new ApertureQuad(
            new Vector2(left, top),
            new Vector2(right, top),
            new Vector2(left, bottom),
            new Vector2(right, bottom));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_zApertureOverlay != null)
        {
            _overlayManager.RemoveOverlay(_zApertureOverlay);
            _zApertureOverlay = null;
        }

        foreach (var target in _zApertureTargets.Values)
            target.Dispose();
        _zApertureTargets.Clear();
    }

    private void EnsureZLevelApertureOverlay()
    {
        if (_zApertureOverlay != null)
            return;

        _zApertureOverlay = new ZLevelApertureOverlay(this);
        _overlayManager.AddOverlay(_zApertureOverlay);
    }

    private sealed partial class ZLevelApertureOverlay : Overlay
    {
        private readonly ScalingViewport _viewport;

        public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

        public ZLevelApertureOverlay(ScalingViewport viewport)
        {
            _viewport = viewport;
            ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.FloorTiles - 1;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            _viewport.DrawZLevelAperturesWorld(args.WorldHandle, args.Viewport);
        }

        protected override bool BeforeDraw(in OverlayDrawArgs args)
        {
            return _viewport._zApertureCaptureThisFrame;
        }
    }

    private readonly record struct ApertureQuad(Vector2 TopLeft, Vector2 TopRight, Vector2 BottomLeft, Vector2 BottomRight)
    {
        public UIBox2 Bounds
        {
            get
            {
                var min = Vector2.Min(Vector2.Min(TopLeft, TopRight), Vector2.Min(BottomLeft, BottomRight));
                var max = Vector2.Max(Vector2.Max(TopLeft, TopRight), Vector2.Max(BottomLeft, BottomRight));
                return new UIBox2(min, max);
            }
        }
    }

    private readonly record struct ZRenderPass(
        int Depth,
        EntityUid MapUid,
        MapCoordinates EyePosition,
        bool DrawFov,
        Vector2 Offset);

    private readonly record struct ZLevelAperture(
        int Depth,
        int SourceDepth,
        ApertureQuad WorldQuad,
        ApertureQuad ViewportQuad);

    public sealed partial class ZEye(int depth) : Robust.Shared.Graphics.Eye
    {
        public int Depth = depth;
        public bool DrawParallax;
    }
}
