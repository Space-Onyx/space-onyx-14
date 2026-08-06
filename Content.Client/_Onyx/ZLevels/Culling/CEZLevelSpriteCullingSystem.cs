using System.Numerics;
using Content.Client._Onyx.ZLevels.Core;
using Content.Shared.CCVar;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Client._Onyx.ZLevels.Culling;

public sealed partial class CEZLevelSpriteCullingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private CEClientZLevelsSystem _zLevels = default!;
    private readonly HashSet<Entity<SpriteComponent>> _candidates = new();
    private readonly HashSet<EntityUid> _hidden = new();
    private readonly HashSet<EntityUid> _stillHidden = new();
    private readonly Dictionary<EntityUid, bool> _originalVisibility = new();
    private readonly List<EntityUid> _restore = new();
    private readonly Robust.Shared.Graphics.Eye _eye = new();
    private EntityQuery<TransformComponent> _xformQuery;
    private uint _lastVisibilitySequence;

    public override void Initialize()
    {
        base.Initialize();
        _zLevels = EntityManager.System<CEClientZLevelsSystem>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        UpdatesBefore.Add(typeof(SpriteTreeSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_config.GetCVar(CCVars.CEZLevelsCullOccludedDynamicSprites))
        {
            RestoreAll();
            return;
        }

        if (_zLevels.RenderVisibilitySequence == _lastVisibilitySequence)
            return;

        _lastVisibilitySequence = _zLevels.RenderVisibilitySequence;
        var currentEye = _eyeManager.CurrentEye;
        if (currentEye.Position != _zLevels.RenderVisibilityBasePosition ||
            currentEye.Offset != _zLevels.RenderVisibilityBaseOffset ||
            currentEye.Rotation != _zLevels.RenderVisibilityBaseRotation ||
            currentEye.Scale != _zLevels.RenderVisibilityBaseScale)
        {
            RestoreAll();
            return;
        }

        _stillHidden.Clear();

        foreach (var visibility in _zLevels.RenderVisibility.Values)
        {
            if (visibility.MapId == MapId.Nullspace || visibility.Regions.Count == 0)
                continue;

            CullMap(visibility);
        }

        RestoreNoLongerHidden();
    }

    private void CullMap(CEClientZLevelsSystem.ZRenderVisibility visibility)
    {
        ConfigureEye(visibility);
        var worldBounds = GetViewportWorldBounds(visibility);
        _candidates.Clear();
        _lookup.GetEntitiesIntersecting(visibility.MapId, worldBounds, _candidates, LookupFlags.Uncontained);

        foreach (var candidate in _candidates)
        {
            var uid = candidate.Owner;
            var sprite = candidate.Comp;
            if (!_xformQuery.TryComp(uid, out var xform) || xform.MapID != visibility.MapId || xform.Anchored)
                continue;

            var hiddenByUs = _hidden.Contains(uid);
            if (!sprite.Visible && !hiddenByUs)
                continue;

            var worldPosition = _transform.GetWorldPosition(xform, _xformQuery);
            var worldRotation = _transform.GetWorldRotation(xform, _xformQuery);
            var spriteBounds = GetSpriteBounds(candidate, worldPosition, worldRotation);
            var screenBounds = WorldToViewport(spriteBounds, visibility);
            if (IntersectsAny(screenBounds.Enlarged(CEClientZLevelsSystem.ZLevelBlurRadius + 2f), visibility.Regions))
            {
                Restore(uid, sprite);
                continue;
            }

            Hide(uid, sprite);
        }
    }

    private Box2 GetSpriteBounds(Entity<SpriteComponent> sprite, Vector2 worldPosition, Angle worldRotation)
    {
        var localBounds = _sprite.GetLocalBounds(sprite);
        var finalRotation = sprite.Comp.NoRotation
            ? sprite.Comp.Rotation - _eye.Rotation
            : sprite.Comp.Rotation + worldRotation;
        var adjustedOffset = sprite.Comp.NoRotation
            ? (-_eye.Rotation).RotateVec(sprite.Comp.Offset)
            : worldRotation.RotateVec(sprite.Comp.Offset);
        var position = worldPosition + adjustedOffset;
        return new Box2Rotated(localBounds.Translated(position), finalRotation, position).CalcBoundingBox();
    }

    private void ConfigureEye(CEClientZLevelsSystem.ZRenderVisibility visibility)
    {
        _eye.Position = visibility.EyePosition;
        _eye.Offset = visibility.EyeOffset;
        _eye.Rotation = visibility.EyeRotation;
        _eye.Scale = visibility.EyeScale;
    }

    private Box2 GetViewportWorldBounds(CEClientZLevelsSystem.ZRenderVisibility visibility)
    {
        var size = (Vector2) visibility.ViewportSize;
        var topLeft = LocalToWorld(Vector2.Zero, visibility);
        var topRight = LocalToWorld(new Vector2(size.X, 0f), visibility);
        var bottomLeft = LocalToWorld(new Vector2(0f, size.Y), visibility);
        var bottomRight = LocalToWorld(size, visibility);
        var min = Vector2.Min(Vector2.Min(topLeft, topRight), Vector2.Min(bottomLeft, bottomRight));
        var max = Vector2.Max(Vector2.Max(topLeft, topRight), Vector2.Max(bottomLeft, bottomRight));
        return new Box2(min, max);
    }

    private Vector2 LocalToWorld(Vector2 point, CEClientZLevelsSystem.ZRenderVisibility visibility)
    {
        point -= (Vector2) visibility.ViewportSize / 2f;
        point *= new Vector2(1f, -1f) / EyeManager.PixelsPerMeter;
        _eye.GetViewMatrixInv(out var matrix, visibility.RenderScale);
        return Vector2.Transform(point, matrix);
    }

    private Box2 WorldToViewport(Box2 bounds, CEClientZLevelsSystem.ZRenderVisibility visibility)
    {
        _eye.GetViewMatrix(out var matrix, visibility.RenderScale * new Vector2(EyeManager.PixelsPerMeter, -EyeManager.PixelsPerMeter));
        matrix.M31 += visibility.ViewportSize.X / 2f;
        matrix.M32 += visibility.ViewportSize.Y / 2f;
        return matrix.TransformBox(bounds);
    }

    private static bool IntersectsAny(Box2 bounds, List<Box2> regions)
    {
        foreach (var region in regions)
        {
            if (bounds.Intersects(region))
                return true;
        }

        return false;
    }

    private void Hide(EntityUid uid, SpriteComponent sprite)
    {
        _stillHidden.Add(uid);
        if (_hidden.Add(uid))
            _originalVisibility[uid] = sprite.Visible;
        if (sprite.Visible)
            _sprite.SetVisible((uid, sprite), false);
    }

    private void Restore(EntityUid uid, SpriteComponent sprite)
    {
        if (!_hidden.Remove(uid))
            return;

        var visible = _originalVisibility.Remove(uid, out var original) && original;
        if (visible && !sprite.Visible)
            _sprite.SetVisible((uid, sprite), true);
    }

    private void RestoreNoLongerHidden()
    {
        _restore.Clear();
        foreach (var uid in _hidden)
        {
            if (!_stillHidden.Contains(uid))
                _restore.Add(uid);
        }

        foreach (var uid in _restore)
        {
            if (TryComp<SpriteComponent>(uid, out var sprite))
                Restore(uid, sprite);
            else
            {
                _hidden.Remove(uid);
                _originalVisibility.Remove(uid);
            }
        }
    }

    private void RestoreAll()
    {
        foreach (var uid in _hidden)
        {
            if (_originalVisibility.TryGetValue(uid, out var original) && original && TryComp<SpriteComponent>(uid, out var sprite) && !sprite.Visible)
                _sprite.SetVisible((uid, sprite), true);
        }

        _hidden.Clear();
        _stillHidden.Clear();
        _originalVisibility.Clear();
    }

    public override void Shutdown()
    {
        RestoreAll();
        base.Shutdown();
    }
}
