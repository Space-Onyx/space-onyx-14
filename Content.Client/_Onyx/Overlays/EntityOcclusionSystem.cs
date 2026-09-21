// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Wall;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using ContentDrawDepth = Content.Shared.DrawDepth.DrawDepth;
using PhysicsTransform = Robust.Shared.Physics.Transform;

namespace Content.Client._Onyx.Overlays;

public sealed partial class EntityOcclusionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private EntityOcclusionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new EntityOcclusionOverlay();
        Subs.CVar(_configuration, CCVars.EntityOcclusionEnabled, OnEnabledChanged, true);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        _overlay.RestoreAll();
        base.Shutdown();
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (enabled)
            _overlays.AddOverlay(_overlay);
        else
        {
            _overlays.RemoveOverlay(_overlay);
            _overlay.RestoreAll();
        }
    }
}

public sealed partial class EntityOcclusionOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly OccluderSystem _occluder;
    private readonly SharedMapSystem _map;
    private readonly SharedPhysicsSystem _physics;
    private readonly SpriteSystem _sprite;
    private readonly SpriteTreeSystem _spriteTree;
    private readonly SharedTransformSystem _transform;

    private readonly EntityQuery<FixturesComponent> _fixturesQuery;
    private readonly EntityQuery<MapComponent> _mapQuery;
    private readonly EntityQuery<WallMountComponent> _wallMountQuery;
    private HashSet<Entity<SpriteComponent, TransformComponent>> _sprites = [];
    private readonly HashSet<Entity<OccluderComponent, TransformComponent>> _occluders = [];
    private readonly HashSet<Entity<OccluderComponent, TransformComponent>> _rayOccluders = [];
    private readonly List<RayOccluder> _rayCandidates = [];
    private readonly Dictionary<EntityUid, (SpriteTreeComponent Tree, Matrix3x2 InverseMatrix)> _spriteTrees = [];
    private readonly Dictionary<EntityUid, float> _originalAlphas = [];
    private readonly HashSet<EntityUid> _seen = [];
    private readonly List<EntityUid> _toRemove = [];

    private const float VisibilitySampleSpacing = 0.0625f;
    private const float TargetInset = 0.001f;
    private const float TargetInsetSquared = TargetInset * TargetInset;
    private const float IntersectionTolerance = 0.0001f;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public EntityOcclusionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _occluder = _entManager.System<OccluderSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _physics = _entManager.System<SharedPhysicsSystem>();
        _sprite = _entManager.System<SpriteSystem>();
        _spriteTree = _entManager.System<SpriteTreeSystem>();
        _transform = _entManager.System<SharedTransformSystem>();
        _fixturesQuery = _entManager.GetEntityQuery<FixturesComponent>();
        _mapQuery = _entManager.GetEntityQuery<MapComponent>();
        _wallMountQuery = _entManager.GetEntityQuery<WallMountComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not { } eye)
        {
            RestoreAll();
            return;
        }

        var mapUid = _map.GetMap(args.MapId);
        if (!_lightManager.Enabled ||
            !_lightManager.DrawHardFov ||
            !eye.DrawLight ||
            !eye.DrawFov ||
            !_mapQuery.TryComp(mapUid, out var map) ||
            !map.LightingEnabled)
        {
            RestoreAll();
            return;
        }

        _sprites.Clear();
        _seen.Clear();
        _occluders.Clear();
        _spriteTrees.Clear();
        var mapId = args.MapId;
        _occluder.QueryAabb(_occluders, mapId, args.WorldBounds, false);
        if (_occluders.Count == 0)
        {
            RestoreAll();
            return;
        }

        foreach (var (uid, tree) in _spriteTree.GetIntersectingTrees(mapId, args.WorldBounds))
        {
            _spriteTrees.Add(uid, (tree, _transform.GetInvWorldMatrix(uid)));
        }

        foreach (var occluder in _occluders)
        {
            var (position, rotation) = _transform.GetWorldPositionRotation(occluder.Comp2);
            var worldBounds = new Box2Rotated(
                occluder.Comp1.LocalBounds.Translated(position),
                rotation,
                position).CalcBoundingBox().Intersect(args.WorldAABB);

            foreach (var (spriteTree, inverseMatrix) in _spriteTrees.Values)
            {
                var localBounds = inverseMatrix.TransformBox(new Box2Rotated(worldBounds, default, default));
                spriteTree.Tree.QueryAabb(ref _sprites,
                    static (ref HashSet<Entity<SpriteComponent, TransformComponent>> sprites,
                        in ComponentTreeEntry<SpriteComponent> entry) =>
                    {
                        sprites.Add(entry);
                        return true;
                    },
                    localBounds,
                    false);
            }
        }

        var localEntity = _player.LocalEntity;
        var wallTopsDepth = (int) ContentDrawDepth.WallTops;
        var eyePosition = eye.Position;

        foreach (var entity in _sprites)
        {
            if (entity.Owner == localEntity ||
                entity.Comp1.DrawDepth <= wallTopsDepth ||
                _wallMountQuery.HasComp(entity.Owner))
            {
                Restore(entity.Owner);
                continue;
            }

            if (IsVisible(eyePosition, entity.Owner, entity.Comp2))
            {
                Restore(entity.Owner);
                continue;
            }

            _seen.Add(entity.Owner);
            if (_originalAlphas.ContainsKey(entity.Owner))
                continue;

            _originalAlphas[entity.Owner] = entity.Comp1.Color.A;
            _sprite.SetColor((entity.Owner, (SpriteComponent?) entity.Comp1), entity.Comp1.Color.WithAlpha(0f));
        }

        _toRemove.Clear();
        foreach (var uid in _originalAlphas.Keys)
        {
            if (!_seen.Contains(uid))
                _toRemove.Add(uid);
        }

        foreach (var uid in _toRemove)
        {
            Restore(uid);
        }
    }

    internal bool IsVisible(MapCoordinates eyePosition, EntityUid target, TransformComponent xform)
    {
        var anchor = _transform.GetMapCoordinates(xform).Position;
        if (!_fixturesQuery.TryComp(target, out var fixtures) || fixtures.Fixtures.Count == 0)
        {
            PrepareRayCandidates(eyePosition, target, new Box2(anchor, anchor));
            return HasLineOfSight(eyePosition.Position, anchor);
        }

        var physicsTransform = default(PhysicsTransform);
        var physicsReady = false;
        var hasHardFixture = false;
        var targetBounds = default(Box2);
        var boundsReady = false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            hasHardFixture = true;
            if (!physicsReady)
            {
                physicsTransform = _physics.GetPhysicsTransform(target, xform);
                physicsReady = true;
            }

            for (var child = 0; child < fixture.Shape.ChildCount; child++)
            {
                var childBounds = fixture.Shape.ComputeAABB(physicsTransform, child);
                targetBounds = boundsReady ? targetBounds.Union(childBounds) : childBounds;
                boundsReady = true;
            }
        }

        if (!hasHardFixture)
        {
            PrepareRayCandidates(eyePosition, target, new Box2(anchor, anchor));
            return HasLineOfSight(eyePosition.Position, anchor);
        }

        PrepareRayCandidates(eyePosition, target, targetBounds);
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard && IsFixtureVisible(eyePosition.Position, fixture.Shape, physicsTransform))
                return true;
        }

        return false;
    }

    private bool IsFixtureVisible(
        Vector2 eyePosition,
        IPhysShape shape,
        PhysicsTransform transform)
    {
        return shape switch
        {
            PhysShapeCircle circle => IsCircleVisible(eyePosition, circle, transform),
            PolygonShape polygon => IsPolygonVisible(eyePosition, polygon, transform),
            PhysShapeAabb aabb => IsAabbVisible(eyePosition, aabb, transform),
            _ => IsShapeBoundsVisible(eyePosition, shape, transform),
        };
    }

    private bool IsShapeBoundsVisible(
        Vector2 eyePosition,
        IPhysShape shape,
        PhysicsTransform transform)
    {
        for (var child = 0; child < shape.ChildCount; child++)
        {
            if (IsBoundsVisible(eyePosition, shape.ComputeAABB(transform, child)))
                return true;
        }

        return false;
    }

    private bool IsCircleVisible(
        Vector2 eyePosition,
        PhysShapeCircle circle,
        PhysicsTransform transform)
    {
        var center = transform.Position + PhysicsTransform.Mul(transform.Quaternion2D, circle.Position);
        if (HasLineOfSight(eyePosition, center))
            return true;

        var samples = Math.Max(8, (int) MathF.Ceiling(MathF.Tau * circle.Radius / VisibilitySampleSpacing));
        for (var i = 0; i < samples; i++)
        {
            var angle = MathF.Tau * i / samples;
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * circle.Radius;
            if (HasLineOfSight(eyePosition, point))
                return true;
        }

        return false;
    }

    private bool IsPolygonVisible(
        Vector2 eyePosition,
        PolygonShape polygon,
        PhysicsTransform transform)
    {
        if (HasLineOfSight(eyePosition, PhysicsTransform.Mul(transform, polygon.Centroid)))
            return true;

        for (var i = 0; i < polygon.Vertices.Length; i++)
        {
            var start = PhysicsTransform.Mul(transform, polygon.Vertices[i]);
            var end = PhysicsTransform.Mul(transform, polygon.Vertices[(i + 1) % polygon.Vertices.Length]);
            if (IsSegmentVisible(eyePosition, start, end))
                return true;
        }

        return false;
    }

    private bool IsAabbVisible(
        Vector2 eyePosition,
        PhysShapeAabb aabb,
        PhysicsTransform transform)
    {
        var bounds = new Box2Rotated(
            aabb.LocalBounds.Translated(transform.Position),
            transform.Quaternion2D.Angle,
            transform.Position);
        bounds.GetCorners(out var bottomLeft, out var bottomRight, out var topRight, out var topLeft);
        return HasLineOfSight(eyePosition, bounds.Center) ||
               IsSegmentVisible(eyePosition, bottomLeft, bottomRight) ||
               IsSegmentVisible(eyePosition, bottomRight, topRight) ||
               IsSegmentVisible(eyePosition, topRight, topLeft) ||
               IsSegmentVisible(eyePosition, topLeft, bottomLeft);
    }

    private bool IsBoundsVisible(Vector2 eyePosition, Box2 bounds)
    {
        return HasLineOfSight(eyePosition, bounds.Center) ||
               IsSegmentVisible(eyePosition, bounds.BottomLeft, bounds.BottomRight) ||
               IsSegmentVisible(eyePosition, bounds.BottomRight, bounds.TopRight) ||
               IsSegmentVisible(eyePosition, bounds.TopRight, bounds.TopLeft) ||
               IsSegmentVisible(eyePosition, bounds.TopLeft, bounds.BottomLeft);
    }

    private bool IsSegmentVisible(Vector2 eyePosition, Vector2 start, Vector2 end)
    {
        var samples = Math.Max(1, (int) MathF.Ceiling(Vector2.Distance(start, end) / VisibilitySampleSpacing));
        for (var i = 0; i <= samples; i++)
        {
            if (HasLineOfSight(eyePosition, Vector2.Lerp(start, end, (float) i / samples)))
                return true;
        }

        return false;
    }

    private void PrepareRayCandidates(MapCoordinates eyePosition, EntityUid target, Box2 targetBounds)
    {
        var eye = eyePosition.Position;
        _rayOccluders.Clear();
        _rayCandidates.Clear();
        _occluder.QueryAabb(
            _rayOccluders,
            eyePosition.MapId,
            targetBounds.ExtendToContain(eye),
            false);

        foreach (var occluder in _rayOccluders)
        {
            if (occluder.Owner == target ||
                _occluder.ContainsPoint(occluder.Comp1, occluder.Comp2, eye))
                continue;

            var worldMatrix = _transform.GetWorldMatrix(occluder.Comp2);
            _rayCandidates.Add(new RayOccluder(
                occluder.Comp1,
                worldMatrix,
                worldMatrix.TransformBox(occluder.Comp1.LocalBounds)));
        }
    }

    private bool HasLineOfSight(Vector2 eyePosition, Vector2 targetPosition)
    {
        var toEye = eyePosition - targetPosition;
        if (toEye.LengthSquared() > TargetInsetSquared)
            targetPosition += toEye.Normalized() * TargetInset;

        var rayBounds = Box2.FromTwoPoints(eyePosition, targetPosition);
        foreach (var occluder in _rayCandidates)
        {
            if (!occluder.WorldBounds.Intersects(rayBounds))
                continue;

            if (IntersectsPolygon(eyePosition, targetPosition, occluder.Component.Polygon, occluder.WorldMatrix))
                return false;
        }

        return true;
    }

    internal static bool IntersectsPolygon(
        Vector2 start,
        Vector2 end,
        ReadOnlySpan<Vector2> polygon,
        Matrix3x2 worldMatrix)
    {
        var previous = Vector2.Transform(polygon[^1], worldMatrix);
        foreach (var vertex in polygon)
        {
            var current = Vector2.Transform(vertex, worldMatrix);
            if (SegmentsIntersect(start, end, previous, current))
                return true;

            previous = current;
        }

        return false;
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var ab = b - a;
        var cd = d - c;
        var denominator = Cross(ab, cd);
        var offset = c - a;

        if (MathF.Abs(denominator) <= IntersectionTolerance)
        {
            if (MathF.Abs(Cross(offset, ab)) > IntersectionTolerance)
                return false;

            var lengthSquared = ab.LengthSquared();
            var first = Vector2.Dot(c - a, ab);
            var second = Vector2.Dot(d - a, ab);
            return MathF.Min(first, second) <= lengthSquared + IntersectionTolerance &&
                   MathF.Max(first, second) >= -IntersectionTolerance;
        }

        var alongRay = Cross(offset, cd) / denominator;
        var alongEdge = Cross(offset, ab) / denominator;
        return alongRay >= -IntersectionTolerance &&
               alongRay <= 1f + IntersectionTolerance &&
               alongEdge >= -IntersectionTolerance &&
               alongEdge <= 1f + IntersectionTolerance;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private readonly record struct RayOccluder(
        OccluderComponent Component,
        Matrix3x2 WorldMatrix,
        Box2 WorldBounds);

    private void Restore(EntityUid uid)
    {
        if (!_originalAlphas.Remove(uid, out var originalAlpha) ||
            !_entManager.TryGetComponent(uid, out SpriteComponent? sprite))
            return;

        _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(originalAlpha));
    }

    public void RestoreAll()
    {
        _toRemove.Clear();
        _toRemove.AddRange(_originalAlphas.Keys);
        foreach (var uid in _toRemove)
        {
            Restore(uid);
        }
    }

    protected override void DisposeBehavior()
    {
        RestoreAll();
        base.DisposeBehavior();
    }
}
