using Content.Shared._Onyx.ZLevels.Elevators;
using JetBrains.Annotations;

namespace Content.Client._Onyx.ZLevels.Elevators;

[UsedImplicitly]
public sealed partial class ElevatorBoundUserInterface : BoundUserInterface
{
    private ElevatorWindow? _window;

    public ElevatorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        if (_window != null)
        {
            _window.OnClose -= Close;
            _window.Dispose();
        }

        _window = new ElevatorWindow();
        _window.OnClose += Close;
        _window.OnFloorSelected += depth => SendMessage(new ElevatorMoveMessage(depth));

        if (State != null)
            UpdateState(State);

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        _window?.UpdateState(state);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
