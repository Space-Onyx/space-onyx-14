using Content.Shared.Shuttles.BUIStates;

namespace Content.Client.Shuttles.UI;

public sealed partial class IFFConsoleWindow
{
    public event Action<Color, string>? ApplyRadarSettings;

    private string _currentGridName = string.Empty;
    private Color _currentColor = Color.Gold;

    private void InitializeIFFSettings()
    {
        ApplySettings.OnPressed += _ => ApplySettingsPressed();
    }

    private void UpdateIFFSettings(IFFConsoleBoundUserInterfaceState state)
    {
        _currentGridName = state.Name;
        _currentColor = state.Color;
        ShuttleName.Text = state.Name;
        ColorPicker.Color = state.Color;
    }

    private void ApplySettingsPressed()
    {
        var name = ShuttleName.Text;
        var color = ColorPicker.Color;
        if (name == _currentGridName && color == _currentColor)
            return;

        ApplyRadarSettings?.Invoke(color, name);
    }
}
