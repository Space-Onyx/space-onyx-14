// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors <https://github.com/AU-14/ColonialMarinesUniverse>
// SPDX-License-Identifier: AGPL-3.0-only

using System.Numerics;
using Content.Client._Onyx.ZLevels.Core;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Onyx.ZLevels.Shooting;

public sealed partial class CMUZLevelClientShootingSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentStartup>(OnSyncedStartup);
        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentShutdown>(OnSyncedShutdown);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentStartup>(OnPredictedStartup);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentShutdown>(OnPredictedShutdown);
    }

    private void OnSyncedStartup(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
    {
        if (!HasComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(ent.Owner))
            TryApplyProjectileVisualOffset(ent.Owner, ent.Comp.Offset, ent.Comp.Depth, ref ent.Comp.OriginalOffset, ref ent.Comp.AppliedOffset);
    }

    private void OnSyncedShutdown(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentShutdown args)
    {
        if (!HasComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(ent.Owner))
            RestoreProjectileVisualOffset(ent.Owner, ent.Comp.OriginalOffset);
    }

    private void OnPredictedStartup(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
    {
        TryApplyProjectileVisualOffset(ent.Owner, ent.Comp.Offset, ent.Comp.Depth, ref ent.Comp.OriginalOffset, ref ent.Comp.AppliedOffset);
    }

    private void OnPredictedShutdown(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentShutdown args)
    {
        RestoreProjectileVisualOffset(ent.Owner, ent.Comp.OriginalOffset);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var syncedQuery = EntityQueryEnumerator<CMUZLevelProjectileVisualOffsetComponent, SpriteComponent, TransformComponent>();
        while (syncedQuery.MoveNext(out var uid, out var visual, out var sprite, out var xform))
        {
            if (!HasComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid))
                ApplyProjectileVisualOffset(uid, visual.Offset, visual.Depth, ref visual.OriginalOffset, ref visual.AppliedOffset, sprite, xform);
        }

        var predictedQuery = EntityQueryEnumerator<CMUZLevelPredictedProjectileVisualOffsetComponent, SpriteComponent, TransformComponent>();
        while (predictedQuery.MoveNext(out var uid, out var visual, out var sprite, out var xform))
            ApplyProjectileVisualOffset(uid, visual.Offset, visual.Depth, ref visual.OriginalOffset, ref visual.AppliedOffset, sprite, xform);
    }

    private bool TryApplyProjectileVisualOffset(EntityUid uid, Vector2 barrelShift, int depth, ref Vector2? originalOffset, ref Vector2 appliedOffset)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || !TryComp(uid, out TransformComponent? xform))
            return false;

        ApplyProjectileVisualOffset(uid, barrelShift, depth, ref originalOffset, ref appliedOffset, sprite, xform);
        return true;
    }

    private void ApplyProjectileVisualOffset(EntityUid uid, Vector2 barrelShift, int depth, ref Vector2? originalOffset, ref Vector2 appliedOffset, SpriteComponent sprite, TransformComponent xform)
    {
        Angle negEyeRotation = _eye.CurrentEye.Rotation * -1;
        var worldOffset = barrelShift + negEyeRotation.ToWorldVec() * CEClientZLevelsSystem.ZLevelOffset * depth;
        Angle renderRotation = sprite.NoRotation
            ? new Angle(_eye.CurrentEye.Rotation * -1)
            : _transformSystem.GetWorldRotation(xform);
        var localVisualOffset = (-renderRotation).RotateVec(worldOffset);

        originalOffset ??= sprite.Offset - appliedOffset;
        if (appliedOffset == localVisualOffset)
            return;

        _sprite.SetOffset((uid, sprite), originalOffset.Value + localVisualOffset);
        appliedOffset = localVisualOffset;
    }

    private void RestoreProjectileVisualOffset(EntityUid uid, Vector2? originalOffset)
    {
        if (originalOffset is { } original && TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetOffset((uid, sprite), original);
    }
}
