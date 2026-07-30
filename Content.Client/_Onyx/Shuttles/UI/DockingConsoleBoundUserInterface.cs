using Content.Shared._Onyx.Shuttles;

namespace Content.Client._Onyx.Shuttles.UI;

public sealed class DockingConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DockingConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = new DockingConsoleWindow(Owner);
        _window.OnFTL += destination => SendMessage(new DockingConsoleFTLMessage(destination));
        _window.OnShuttleCall += () => SendMessage(new DockingConsoleShuttleCheckMessage());
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DockingConsoleState docking)
            _window?.UpdateState(docking);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Orphan();
    }
}
