using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.VendingMachines.Components;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client.VendingMachines;

public sealed partial class VendingMachineSystem : SharedVendingMachineSystem
{
    [Dependency] private AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void UpdateUI(Entity<VendingMachineComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp)) return;
    }

    protected override void OnEjectStateChanged(Entity<VendingMachineComponent?> entity, VendingMachineEjectComponent? ejectComponent = null)
    {
        TryUpdateVisualState(entity, ejectComponent);
    }

    [SubscribeLocalEvent]
    private void OnVendingHandleState(Entity<VendingMachineComponent> entity, ref ComponentHandleState args)
    {
        if (args.Current is not VendingMachineComponentState state) return;
        var component = entity.Comp;
        component.Contraband = state.Contraband;
        var brokenChanged = component.Broken != state.Broken;
        component.Broken = state.Broken;
        component.AllForFree = state.AllForFree;
        component.UiButtonBorderColor = state.UiButtonBorderColor;
        component.UiButtonBaseColor = state.UiButtonBaseColor;
        component.UiButtonHoveredColor = state.UiButtonHoveredColor;
        component.UiButtonDisabledColor = state.UiButtonDisabledColor;
        CopyInventory(state.Inventory, component.Inventory);
        CopyInventory(state.EmaggedInventory, component.EmaggedInventory);
        CopyInventory(state.ContrabandInventory, component.ContrabandInventory);
        if (brokenChanged) TryUpdateVisualState((entity.Owner, component));
    }

    [SubscribeLocalEvent]
    private void OnEjectHandleState(Entity<VendingMachineEjectComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        TryUpdateVisualState(entity.Owner);
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<VendingMachineComponent> entity, ref PowerChangedEvent args)
    {
        TryUpdateVisualState((entity.Owner, entity.Comp));
    }

    [SubscribeLocalEvent]
    private void OnAnimationCompleted(EntityUid uid, VendingMachineVisualsComponent visuals, AnimationCompletedEvent args)
    {
        if (!TryComp<VendingMachineComponent>(uid, out var vend) || !TryComp<SpriteComponent>(uid, out var sprite)) return;
        TryComp<VendingMachineEjectComponent>(uid, out var eject);
        UpdateAppearance(uid, GetVisualState(uid, vend, eject), visuals, eject, sprite);
    }

    [SubscribeLocalEvent]
    private void OnVisualsStartup(Entity<VendingMachineVisualsComponent> entity, ref ComponentStartup args) => TryUpdateVisualState(entity.Owner);

    private void TryUpdateVisualState(Entity<VendingMachineComponent?> entity, VendingMachineEjectComponent? ejectComponent = null)
    {
        if (!Resolve(entity.Owner, ref entity.Comp)) return;
        Resolve(entity.Owner, ref ejectComponent, false);
        if (!TryComp<VendingMachineVisualsComponent>(entity.Owner, out var visuals) || !TryComp<SpriteComponent>(entity.Owner, out var sprite)) return;
        var state = GetVisualState(entity.Owner, entity.Comp, ejectComponent);
        UpdatePointLight(entity.Owner, state);
        UpdateAppearance(entity.Owner, state, visuals, ejectComponent, sprite);
    }

    private VendingMachineVisualState GetVisualState(EntityUid uid, VendingMachineComponent vend, VendingMachineEjectComponent? eject) =>
        vend.Broken ? VendingMachineVisualState.Broken : eject?.Ejecting == true ? VendingMachineVisualState.Eject : eject?.Denying == true ? VendingMachineVisualState.Deny : !_receiver.IsPowered(uid) ? VendingMachineVisualState.Off : VendingMachineVisualState.Normal;

    private void UpdatePointLight(EntityUid uid, VendingMachineVisualState state)
    {
        if (_light.TryGetLight(uid, out var light)) _light.SetEnabled(uid, state is not VendingMachineVisualState.Broken and not VendingMachineVisualState.Off, light);
    }

    private void UpdateAppearance(EntityUid uid, VendingMachineVisualState state, VendingMachineVisualsComponent visuals, VendingMachineEjectComponent? eject, SpriteComponent sprite)
    {
        SetLayerState(VendingMachineVisualLayers.Base, visuals.OffState, (uid, sprite));
        switch (state)
        {
            case VendingMachineVisualState.Normal:
                SetLayerState(VendingMachineVisualLayers.BaseUnshaded, visuals.NormalState, (uid, sprite)); SetLayerState(VendingMachineVisualLayers.Screen, visuals.ScreenState, (uid, sprite)); break;
            case VendingMachineVisualState.Deny:
                if (visuals.LoopDenyAnimation || eject == null) SetLayerState(VendingMachineVisualLayers.BaseUnshaded, visuals.DenyState, (uid, sprite)); else PlayAnimation(uid, VendingMachineVisualLayers.BaseUnshaded, visuals.DenyState, (float)eject.DenyDelay.TotalSeconds, sprite);
                SetLayerState(VendingMachineVisualLayers.Screen, visuals.ScreenState, (uid, sprite)); break;
            case VendingMachineVisualState.Eject:
                if (eject == null) SetLayerState(VendingMachineVisualLayers.BaseUnshaded, visuals.EjectState, (uid, sprite)); else PlayAnimation(uid, VendingMachineVisualLayers.BaseUnshaded, visuals.EjectState, (float)eject.EjectDelay.TotalSeconds, sprite);
                SetLayerState(VendingMachineVisualLayers.Screen, visuals.ScreenState, (uid, sprite)); break;
            case VendingMachineVisualState.Broken: HideLayers((uid, sprite)); SetLayerState(VendingMachineVisualLayers.Base, visuals.BrokenState, (uid, sprite)); break;
            case VendingMachineVisualState.Off: HideLayers((uid, sprite)); break;
        }
    }

    private void SetLayerState(VendingMachineVisualLayers layer, string? state, Entity<SpriteComponent> sprite)
    {
        if (string.IsNullOrEmpty(state)) return;
        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true); _sprite.LayerSetAutoAnimated(sprite.AsNullable(), layer, true); _sprite.LayerSetRsiState(sprite.AsNullable(), layer, state);
    }

    private void PlayAnimation(EntityUid uid, VendingMachineVisualLayers layer, string? state, float time, SpriteComponent sprite)
    {
        if (string.IsNullOrEmpty(state) || _animationPlayer.HasRunningAnimation(uid, state)) return;
        _sprite.LayerSetVisible((uid, sprite), layer, true); _animationPlayer.Play(uid, new Animation { Length = TimeSpan.FromSeconds(time), AnimationTracks = { new AnimationTrackSpriteFlick { LayerKey = layer, KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(state, 0f) } } } }, state);
    }

    private void HideLayers(Entity<SpriteComponent> sprite) { HideLayer(VendingMachineVisualLayers.BaseUnshaded, sprite); HideLayer(VendingMachineVisualLayers.Screen, sprite); }
    private void HideLayer(VendingMachineVisualLayers layer, Entity<SpriteComponent> sprite)
    {
        if (_sprite.LayerMapTryGet(sprite.AsNullable(), layer, out var actualLayer, false)) _sprite.LayerSetVisible(sprite.AsNullable(), actualLayer, false);
    }

    private bool TryGetOpenUi(EntityUid uid, [NotNullWhen(true)] out VendingMachineBoundUserInterface? bui) => UISystem.TryGetOpenUi(uid, VendingMachineUiKey.Key, out bui);
}
