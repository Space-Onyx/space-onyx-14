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
    private readonly Dictionary<EntityUid, (SpriteTreeComponent Tree, Matrix3x2 InverseMatrix)> _spriteTrees = [];
    private readonly Dictionary<EntityUid, float> _originalAlphas = [];
    private readonly HashSet<EntityUid> _seen = [];
    private readonly List<EntityUid> _toRemove = [];

    private const float TargetInset = 0.01f;

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
        _occluder.QueryAabb(_occluders, args.MapId, args.WorldBounds, false);
        foreach (var (uid, tree) in _spriteTree.GetIntersectingTrees(args.MapId, args.WorldBounds))
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

        var eyePosition = eye.Position;

        foreach (var entity in _sprites)
        {
            if (entity.Owner == _player.LocalEntity ||
                entity.Comp1.DrawDepth <= (int) ContentDrawDepth.WallTops ||
                _wallMountQuery.HasComp(entity.Owner))
            {
                Restore(entity.Owner);
                continue;
            }

            var visible = IsVisible(eyePosition, entity.Owner, entity.Comp2);

            if (visible)
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

    private bool IsVisible(MapCoordinates eyePosition, EntityUid target, TransformComponent xform)
    {
        if (!_fixturesQuery.TryComp(target, out var fixtures) || fixtures.Fixtures.Count == 0)
            return HasLineOfSight(eyePosition, _transform.GetMapCoordinates(xform).Position, target);

        var physicsTransform = _physics.GetPhysicsTransform(target, xform);
        var hasHardFixture = false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            hasHardFixture = true;
            if (IsFixtureVisible(eyePosition, fixture.Shape, physicsTransform, target))
                return true;
        }

        return !hasHardFixture &&
               HasLineOfSight(eyePosition, _transform.GetMapCoordinates(xform).Position, target);
    }

    private bool IsFixtureVisible(
        MapCoordinates eyePosition,
        IPhysShape shape,
        PhysicsTransform transform,
        EntityUid target)
    {
        return shape switch
        {
            PhysShapeCircle circle => IsCircleVisible(eyePosition, circle, transform, target),
            PolygonShape polygon => IsPolygonVisible(eyePosition, polygon, transform, target),
            PhysShapeAabb aabb => IsAabbVisible(eyePosition, aabb, transform, target),
            _ => IsShapeBoundsVisible(eyePosition, shape, transform, target),
        };
    }

    private bool IsShapeBoundsVisible(
        MapCoordinates eyePosition,
        IPhysShape shape,
        PhysicsTransform transform,
        EntityUid target)
    {
        for (var child = 0; child < shape.ChildCount; child++)
        {
            if (IsBoundsVisible(eyePosition, shape.ComputeAABB(transform, child), target))
                return true;
        }

        return false;
    }

    private bool IsCircleVisible(
        MapCoordinates eyePosition,
        PhysShapeCircle circle,
        PhysicsTransform transform,
        EntityUid target)
    {
        var center = transform.Position + PhysicsTransform.Mul(transform.Quaternion2D, circle.Position);
        if (HasLineOfSight(eyePosition, center, target))
            return true;

        var eyeToCenter = center - eyePosition.Position;
        var distanceSquared = eyeToCenter.LengthSquared();
        var radiusSquared = circle.Radius * circle.Radius;
        if (distanceSquared <= radiusSquared)
            return true;

        var distance = MathF.Sqrt(distanceSquared);
        var direction = eyeToCenter / distance;
        var along = radiusSquared / distance;
        var perpendicular = circle.Radius * MathF.Sqrt(distanceSquared - radiusSquared) / distance;
        var tangentBase = center - direction * along;
        var side = new Vector2(-direction.Y, direction.X) * perpendicular;
        return HasLineOfSight(eyePosition, tangentBase + side, target) ||
               HasLineOfSight(eyePosition, tangentBase - side, target);
    }

    private bool IsPolygonVisible(
        MapCoordinates eyePosition,
        PolygonShape polygon,
        PhysicsTransform transform,
        EntityUid target)
    {
        if (HasLineOfSight(eyePosition, PhysicsTransform.Mul(transform, polygon.Centroid), target))
            return true;

        for (var i = 0; i < polygon.Vertices.Length; i++)
        {
            var vertex = polygon.Vertices[i];
            var next = polygon.Vertices[(i + 1) % polygon.Vertices.Length];
            if (HasLineOfSight(eyePosition, PhysicsTransform.Mul(transform, vertex), target) ||
                HasLineOfSight(eyePosition, PhysicsTransform.Mul(transform, (vertex + next) / 2f), target))
                return true;
        }

        return false;
    }

    private bool IsAabbVisible(
        MapCoordinates eyePosition,
        PhysShapeAabb aabb,
        PhysicsTransform transform,
        EntityUid target)
    {
        var bounds = new Box2Rotated(
            aabb.LocalBounds.Translated(transform.Position),
            transform.Quaternion2D.Angle,
            transform.Position);
        bounds.GetCorners(out var bottomLeft, out var bottomRight, out var topRight, out var topLeft);
        return HasLineOfSight(eyePosition, bounds.Center, target) ||
               HasLineOfSight(eyePosition, bottomLeft, target) ||
               HasLineOfSight(eyePosition, (bottomLeft + bottomRight) / 2f, target) ||
               HasLineOfSight(eyePosition, bottomRight, target) ||
               HasLineOfSight(eyePosition, (bottomRight + topRight) / 2f, target) ||
               HasLineOfSight(eyePosition, topRight, target) ||
               HasLineOfSight(eyePosition, (topRight + topLeft) / 2f, target) ||
               HasLineOfSight(eyePosition, topLeft, target) ||
               HasLineOfSight(eyePosition, (topLeft + bottomLeft) / 2f, target);
    }

    private bool IsBoundsVisible(MapCoordinates eyePosition, Box2 bounds, EntityUid target)
    {
        return HasLineOfSight(eyePosition, bounds.Center, target) ||
               HasLineOfSight(eyePosition, bounds.BottomLeft, target) ||
               HasLineOfSight(eyePosition, (bounds.BottomLeft + bounds.BottomRight) / 2f, target) ||
               HasLineOfSight(eyePosition, bounds.BottomRight, target) ||
               HasLineOfSight(eyePosition, (bounds.BottomRight + bounds.TopRight) / 2f, target) ||
               HasLineOfSight(eyePosition, bounds.TopRight, target) ||
               HasLineOfSight(eyePosition, (bounds.TopRight + bounds.TopLeft) / 2f, target) ||
               HasLineOfSight(eyePosition, bounds.TopLeft, target) ||
               HasLineOfSight(eyePosition, (bounds.TopLeft + bounds.BottomLeft) / 2f, target);
    }

    private bool HasLineOfSight(MapCoordinates eyePosition, Vector2 targetPosition, EntityUid target)
    {
        var toEye = eyePosition.Position - targetPosition;
        if (toEye.LengthSquared() > TargetInset * TargetInset)
            targetPosition += toEye.Normalized() * TargetInset;

        return _occluder.InRangeUnoccluded(
            eyePosition,
            new MapCoordinates(targetPosition, eyePosition.MapId),
            0f,
            target,
            static (occluder, targetEntity) => occluder.Owner == targetEntity);
    }

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
