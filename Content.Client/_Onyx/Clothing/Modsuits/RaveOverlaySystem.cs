using Content.Client.Overlays;
using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client._Onyx.Clothing.Modsuits;

public sealed partial class RaveOverlaySystem : EquipmentHudSystem<RaveOverlayComponent>
{
    [Dependency] private IOverlayManager _overlays = default!;
    private RaveOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new RaveOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<RaveOverlayComponent> component)
    {
        base.UpdateInternal(component);
        _overlay.UpdateParameters(component.Components[0]);
        _overlays.AddOverlay(_overlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();
        _overlays.RemoveOverlay(_overlay);
    }
}
