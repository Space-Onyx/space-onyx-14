using Content.Shared.Shuttles.Events;

namespace Content.Client.Shuttles.BUI;

public sealed partial class IFFConsoleBoundUserInterface
{
    private void InitializeIFFSettings()
    {
        _window!.ApplyRadarSettings += SendRadarSettingsMessage;
    }

    private void SendRadarSettingsMessage(Color color, string name)
    {
        SendMessage(new IFFApplyRadarSettingsMessage
        {
            Color = color,
            Name = name,
        });
    }
}
