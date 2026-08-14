using System.Linq;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void InitializeCybernetics()
    {
        CyberneticsPicker.SelectionChanged += selected =>
        {
            if (Profile == null)
                return;

            Profile = Profile.WithCybernetics(selected);
            ReloadPreview();
        };
    }

    private void RefreshCybernetics()
    {
        if (Profile == null || !_prototypeManager.TryIndex(Profile.Species, out var species))
            return;

        var normalized = CyberneticsPicker.SetData(Profile.Cybernetics, species.RoundstartCyberwareCapacity);
        if (!normalized.SequenceEqual(Profile.Cybernetics))
            Profile = Profile.WithCybernetics(normalized);
    }
}
