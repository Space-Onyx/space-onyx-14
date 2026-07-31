using Content.Client._Onyx.Xenomorphs.RadialSelector;
using Content.Shared._Onyx.Xenomorphs.RadialSelector;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace Content.Client._Onyx.Xenomorphs.RadialSelector;

[UsedImplicitly]
public sealed partial class TrackedRadialSelectorMenuBUI(EntityUid owner, Enum uiKey) : BasedRadialSelectorMenuBUI(owner, uiKey)
{
    private readonly TrackedRadialMenu _menu = new()
    {
        HorizontalExpand = true,
        VerticalExpand = true,
        BackButtonStyleClass = "RadialMenuBackButton",
        CloseButtonStyleClass = "RadialMenuCloseButton"
    };

    private EntityUid? _trackedEntity;

    protected override void Open()
    {
        base.Open();

        _menu.OnClose += Close;
        _menu.Track(_trackedEntity ?? Owner);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not TrackedRadialSelectorState trackedRadialSelectorState)
            return;

        ClearExistingContainers(_menu);
        CreateMenu(trackedRadialSelectorState.Entries, _menu);

        _trackedEntity = EntMan.GetEntity(trackedRadialSelectorState.TrackedEntity);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _menu.Dispose();
    }
}
