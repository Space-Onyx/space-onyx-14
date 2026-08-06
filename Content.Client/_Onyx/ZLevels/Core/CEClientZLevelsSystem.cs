/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client.Damage.Systems;
using Content.Shared._Onyx.Carrying;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Content.Shared._Onyx.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Onyx.ZLevels.Core;

public sealed partial class CEClientZLevelsSystem : CESharedZLevelsSystem
{
    private const string StaminaAnimationKey = "stamina";
    private bool _clientInitialized;

    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public static float ZLevelOffset = 0.3f;
    public static float ZLevelBlurRadius = 2f;
    public uint RenderVisibilitySequence { get; private set; }
    public readonly Dictionary<int, ZRenderVisibility> RenderVisibility = new();
    public MapCoordinates RenderVisibilityBasePosition { get; private set; }
    public Vector2 RenderVisibilityBaseOffset { get; private set; }
    public Angle RenderVisibilityBaseRotation { get; private set; }
    public Vector2 RenderVisibilityBaseScale { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        if (_clientInitialized)
            return;

        _clientInitialized = true;
        _overlay.AddOverlay(new CEZLevelBlurOverlay());
        Subs.CVar(_cfg, CCVars.CEZLevelsRenderOffset, value => ZLevelOffset = MathF.Max(0f, value), true);
        Subs.CVar(_cfg, CCVars.CEZLevelsBlurRadius, value => ZLevelBlurRadius = Math.Clamp(value, 0f, 10f), true);

        SubscribeLocalEvent<CEZPhysicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEZPhysicsComponent, AfterAutoHandleStateEvent>(OnZPhysicsHandleState);
        SubscribeLocalEvent<CEZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<CEZPhysicsComponent, CEZPhysicsActivationChangedEvent>(OnActivationChanged);
        SubscribeLocalEvent<CEZItemPhysicsComponent, ComponentStartup>(OnItemZPhysicsStartup);
        SubscribeLocalEvent<CEZItemPhysicsComponent, ComponentRemove>(OnItemZPhysicsRemove);
    }

    private void OnEyeOffset(Entity<CEZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var localPosition = GetVisualsLocalPosition((ent, ent), Transform(ent));
        args.Offset += rotation.RotateVec(new Vector2(0, localPosition * ZLevelOffset));
    }

    private void OnStartup(Entity<CEZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ent.Comp.NoRotDefault = sprite.NoRotation;
        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
    }

    private void OnZPhysicsHandleState(Entity<CEZPhysicsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!ZDebugStairsEnabled || _player.LocalEntity != ent.Owner)
            return;

        DebugZStairCsv(ent,
            "client_z_state_handle",
            $"state={args.State.GetType().Name},local={StairCsvFloat(ent.Comp.LocalPosition)},vel={StairCsvFloat(ent.Comp.Velocity)},current_z={ent.Comp.CurrentZLevel}",
            $"{args.State.GetType().Name}|{StairCsvFloat(ent.Comp.LocalPosition)}|{StairCsvFloat(ent.Comp.Velocity)}|{ent.Comp.CurrentZLevel}|{Transform(ent).ParentUid}|{Transform(ent).GridUid}|{Transform(ent).MapUid}");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var uid in ActiveBodies)
        {
            if (!ZPhysQuery.TryComp(uid, out var zPhys) ||
                !TryComp<SpriteComponent>(uid, out var sprite) ||
                !TransformQuery.TryComp(uid, out var xform))
                continue;

            var localPosition = GetVisualsLocalPosition((uid, zPhys), xform);
            var noRotation = localPosition != 0 || zPhys.NoRotDefault;
            if (sprite.NoRotation != noRotation)
                sprite.NoRotation = noRotation;
            var offset = zPhys.SpriteOffsetDefault + new Vector2(0, localPosition * ZLevelOffset);
            if (sprite.Offset != offset)
                _sprite.SetOffset((uid, sprite), offset);
            var drawDepth = localPosition > 0 ? (int) Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault;
            if (sprite.DrawDepth != drawDepth)
                _sprite.SetDrawDepth((uid, sprite), drawDepth);
        }

        var staminaQuery = EntityQueryEnumerator<StaminaComponent, SpriteComponent, CEZPhysicsComponent>();
        while (staminaQuery.MoveNext(out var uid, out var stamina, out var sprite, out _))
        {
            if (_animation.HasRunningAnimation(uid, StaminaAnimationKey))
                stamina.StartOffset = sprite.Offset;
        }

        var itemQuery = EntityQueryEnumerator<CEZItemPhysicsComponent, SpriteComponent>();
        while (itemQuery.MoveNext(out var uid, out var zItem, out var sprite))
        {
            var localPosition = MathF.Max(zItem.LocalPosition, 0f);
            if (localPosition <= 0f)
            {
                if (zItem.VisualsApplied)
                {
                    if (sprite.NoRotation != zItem.NoRotDefault)
                        sprite.NoRotation = zItem.NoRotDefault;
                    if (sprite.Offset != zItem.SpriteOffsetDefault)
                        _sprite.SetOffset((uid, sprite), zItem.SpriteOffsetDefault);
                    if (sprite.DrawDepth != zItem.DrawDepthDefault)
                        _sprite.SetDrawDepth((uid, sprite), zItem.DrawDepthDefault);
                    zItem.VisualsApplied = false;
                }
                continue;
            }

            EnsureItemVisualDefaults((uid, zItem), sprite);
            if (!sprite.NoRotation)
                sprite.NoRotation = true;
            var offset = zItem.SpriteOffsetDefault + new Vector2(0, localPosition * ZLevelOffset);
            if (sprite.Offset != offset)
                _sprite.SetOffset((uid, sprite), offset);
            var drawDepth = (int) Shared.DrawDepth.DrawDepth.OverMobs;
            if (sprite.DrawDepth != drawDepth)
                _sprite.SetDrawDepth((uid, sprite), drawDepth);
            zItem.VisualsApplied = true;
        }

        var carriedQuery = EntityQueryEnumerator<BeingCarriedComponent, SpriteComponent, CEZPhysicsComponent>();
        while (carriedQuery.MoveNext(out var uid, out var carried, out var sprite, out var zPhys))
        {
            if (!ZPhysQuery.TryComp(carried.Carrier, out var carrierZ) ||
                !TransformQuery.TryComp(carried.Carrier, out var carrierXform))
                continue;

            var localPosition = GetVisualsLocalPosition((carried.Carrier, carrierZ), carrierXform);
            var offset = zPhys.SpriteOffsetDefault + new Vector2(0, localPosition * ZLevelOffset);
            if (sprite.Offset != offset)
                _sprite.SetOffset((uid, sprite), offset);
            var drawDepth = localPosition > 0 ? (int) Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault;
            if (sprite.DrawDepth != drawDepth)
                _sprite.SetDrawDepth((uid, sprite), drawDepth);
        }
    }

    private void OnActivationChanged(Entity<CEZPhysicsComponent> ent, ref CEZPhysicsActivationChangedEvent args)
    {
        if (args.Active || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.NoRotation = ent.Comp.NoRotDefault;
        _sprite.SetOffset((ent.Owner, sprite), ent.Comp.SpriteOffsetDefault);
        _sprite.SetDrawDepth((ent.Owner, sprite), ent.Comp.DrawDepthDefault);
    }

    private void OnItemZPhysicsStartup(Entity<CEZItemPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            EnsureItemVisualDefaults(ent, sprite);
    }

    private void OnItemZPhysicsRemove(Entity<CEZItemPhysicsComponent> ent, ref ComponentRemove args)
    {
        if (!ent.Comp.VisualsInitialized || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.NoRotation = ent.Comp.NoRotDefault;
        _sprite.SetOffset((ent.Owner, sprite), ent.Comp.SpriteOffsetDefault);
        _sprite.SetDrawDepth((ent.Owner, sprite), ent.Comp.DrawDepthDefault);
    }

    private void EnsureItemVisualDefaults(Entity<CEZItemPhysicsComponent> ent, SpriteComponent sprite)
    {
        if (ent.Comp.VisualsInitialized)
            return;

        ent.Comp.NoRotDefault = sprite.NoRotation;
        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
        ent.Comp.VisualsInitialized = true;
    }

    public void PublishRenderVisibility(Dictionary<int, ZRenderVisibility> visibility, IEye baseEye)
    {
        foreach (var entry in RenderVisibility.Values)
            entry.Regions.Clear();

        foreach (var (depth, source) in visibility)
        {
            if (!RenderVisibility.TryGetValue(depth, out var target))
            {
                target = new ZRenderVisibility();
                RenderVisibility[depth] = target;
            }

            target.MapId = source.MapId;
            target.EyePosition = source.EyePosition;
            target.EyeOffset = source.EyeOffset;
            target.EyeRotation = source.EyeRotation;
            target.EyeScale = source.EyeScale;
            target.ViewportSize = source.ViewportSize;
            target.RenderScale = source.RenderScale;
            target.Regions.AddRange(source.Regions);
        }

        RenderVisibilityBasePosition = baseEye.Position;
        RenderVisibilityBaseOffset = baseEye.Offset;
        RenderVisibilityBaseRotation = baseEye.Rotation;
        RenderVisibilityBaseScale = baseEye.Scale;
        RenderVisibilitySequence++;
    }

    public void ClearRenderVisibility()
    {
        foreach (var entry in RenderVisibility.Values)
            entry.Regions.Clear();
        RenderVisibilitySequence++;
    }

    public sealed class ZRenderVisibility
    {
        public MapId MapId;
        public MapCoordinates EyePosition;
        public Vector2 EyeOffset;
        public Angle EyeRotation;
        public Vector2 EyeScale;
        public Vector2i ViewportSize;
        public Vector2 RenderScale;
        public readonly List<Box2> Regions = new();
    }

    public float GetVisualsLocalPosition(Entity<CEZPhysicsComponent?> ent, TransformComponent? xform = null)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !Resolve(ent, ref xform, false))
            return 0;

        var pos = ent.Comp.LocalPosition;
        if (xform.ParentUid != xform.MapUid && ZPhysQuery.TryComp(xform.ParentUid, out var parentZPhys))
            pos = parentZPhys.LocalPosition;

        return pos;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CEZLevelBlurOverlay>();
    }
}
