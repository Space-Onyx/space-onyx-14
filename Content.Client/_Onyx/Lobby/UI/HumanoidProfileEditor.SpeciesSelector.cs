using Content.Client._Onyx.Lobby.UI;
using Content.Client.Guidebook;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private SpeciesWindow? _speciesWindow;

    private void InitializeSpeciesSelector()
    {
        RefreshSpeciesSelector();
        SpeciesButton.OnToggled += args =>
        {
            if (Profile == null)
                return;

            _speciesWindow?.Dispose();
            if (!args.Pressed)
            {
                _speciesWindow = null;
                return;
            }

            var documentParser = IoCManager.Resolve<DocumentParsingManager>();
            _speciesWindow = new SpeciesWindow(Profile, _prototypeManager, _resManager, documentParser);
            _speciesWindow.ChooseAction += species =>
            {
                SetSpecies(species);
                SpeciesButton.Text = Loc.GetString(_prototypeManager.Index<SpeciesPrototype>(species).Name);
                SpeciesButton.Pressed = false;
                _speciesWindow?.Close();
            };
            _speciesWindow.OnClose += () =>
            {
                SpeciesButton.Pressed = false;
                _speciesWindow = null;
            };
            _speciesWindow.OpenCenteredLeft();
        };
    }

    private void RefreshSpeciesSelector()
    {
        if (Profile == null)
            return;

        // Only reset when the prototype is gone entirely. Non-RoundStart species
        // are left untouched here; EnsureValid normalizes them on save.
        if (!_prototypeManager.HasIndex<SpeciesPrototype>(Profile.Species))
            SetSpecies(HumanoidCharacterProfile.DefaultSpecies);

        if (_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var currentSpecies))
            SpeciesButton.Text = Loc.GetString(currentSpecies.Name);
    }

    private void DisposeSpeciesSelector()
    {
        _speciesWindow?.Dispose();
        _speciesWindow = null;
    }
}
