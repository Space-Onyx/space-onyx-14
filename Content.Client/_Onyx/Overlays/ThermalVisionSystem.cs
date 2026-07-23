using Content.Client.Overlays;
using Content.Shared._Onyx.Overlays;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client._Onyx.Overlays;

public sealed partial class ThermalVisionSystem : EquipmentHudSystem<ThermalVisionComponent>
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private ThermalVisionOverlay _overlay = default!;
    private ThermalVisionScreenOverlay _screenOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new ThermalVisionOverlay();
        _screenOverlay = new ThermalVisionScreenOverlay();
        SubscribeLocalEvent<ThermalVisionComponent, AfterAutoHandleStateEvent>(OnState);
    }

    protected override void OnRefreshComponentHud(Entity<ThermalVisionComponent> ent,
        ref RefreshEquipmentHudEvent<ThermalVisionComponent> args)
    {
        if (!ent.Comp.Enabled)
            return;

        base.OnRefreshComponentHud(ent, ref args);
    }

    protected override void OnRefreshEquipmentHud(Entity<ThermalVisionComponent> ent,
        ref InventoryRelayedEvent<RefreshEquipmentHudEvent<ThermalVisionComponent>> args)
    {
        if (!ent.Comp.Enabled)
            return;

        base.OnRefreshEquipmentHud(ent, ref args);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ThermalVisionComponent> args)
    {
        base.UpdateInternal(args);

        var comp = args.Components[0];
        _overlay.Component = comp;
        _screenOverlay.Component = comp;
        if (!_overlayManager.HasOverlay<ThermalVisionOverlay>())
            _overlayManager.AddOverlay(_overlay);
        if (!_overlayManager.HasOverlay<ThermalVisionScreenOverlay>())
            _overlayManager.AddOverlay(_screenOverlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();
        _overlayManager.RemoveOverlay(_overlay);
        _overlayManager.RemoveOverlay(_screenOverlay);
        _overlay.ResetLight();
    }

    private void OnState(Entity<ThermalVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }
}
