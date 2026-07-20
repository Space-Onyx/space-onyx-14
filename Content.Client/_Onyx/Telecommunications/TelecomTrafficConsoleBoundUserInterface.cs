using Content.Shared._Onyx.Telecommunications;

namespace Content.Client._Onyx.Telecommunications;

public sealed class TelecomTrafficConsoleBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private TelecomTrafficConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = new TelecomTrafficConsoleWindow();
        _window.OnServerSelected += server => SendMessage(new TelecomTrafficSelectServerMessage(server));
        _window.OnRoutingToggled += enabled => SendMessage(new TelecomTrafficToggleRoutingMessage(enabled));
        _window.OnChannelToggled += channel => SendMessage(new TelecomTrafficToggleChannelMessage(channel));
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        _window?.UpdateState((TelecomTrafficConsoleState) state);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is TelecomTrafficTelemetryMessage telemetry)
            _window?.UpdateTelemetry(telemetry);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
