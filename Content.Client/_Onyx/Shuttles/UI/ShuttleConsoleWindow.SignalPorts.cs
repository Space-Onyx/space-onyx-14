namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleConsoleWindow
{
    public event Action<string>? NetworkPortButtonPressed;

    private void InitializeSignalPorts()
    {
        NavContainer.NetworkPortButtonPressed += port => NetworkPortButtonPressed?.Invoke(port);
    }
}
