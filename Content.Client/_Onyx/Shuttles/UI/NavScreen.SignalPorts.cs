namespace Content.Client.Shuttles.UI;

public sealed partial class NavScreen
{
    public event Action<string>? NetworkPortButtonPressed;

    private void InitializeSignalPorts()
    {
        DeviceButton1.OnPressed += _ => NetworkPortButtonPressed?.Invoke("SignalShuttleConsole1");
        DeviceButton2.OnPressed += _ => NetworkPortButtonPressed?.Invoke("SignalShuttleConsole2");
        DeviceButton3.OnPressed += _ => NetworkPortButtonPressed?.Invoke("SignalShuttleConsole3");
        DeviceButton4.OnPressed += _ => NetworkPortButtonPressed?.Invoke("SignalShuttleConsole4");
    }
}
