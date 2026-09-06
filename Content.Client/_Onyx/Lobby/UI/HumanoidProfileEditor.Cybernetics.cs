using System.Linq;
using Content.Shared.CCVar;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void InitializeCybernetics()
    {
        if (!_cfgManager.GetCVar(CCVars.RoundstartCyberneticsEnabled))
        {
            TabContainer.RemoveChild(CyberneticsTab);
            return;
        }

        CyberneticsPicker.SelectionChanged += selected =>
        {
            if (Profile == null || _settingProfile)
                return;

            Profile = Profile.WithCybernetics(selected);
            ReloadPreview();
        };
    }

    private void RefreshCybernetics()
    {
        if (!_cfgManager.GetCVar(CCVars.RoundstartCyberneticsEnabled) ||
            Profile == null ||
            !_prototypeManager.TryIndex(Profile.Species, out var species))
            return;

        var normalized = CyberneticsPicker.SetData(Profile.Cybernetics, species.RoundstartCyberwareCapacity);
        if (!normalized.SequenceEqual(Profile.Cybernetics))
            Profile = Profile.WithCybernetics(normalized);
    }
}
