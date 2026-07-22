using Content.Client.Guidebook;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface.Systems.EscapeMenu;

public sealed partial class EscapeUIController
{
    [Dependency] private IClientPreferencesManager _preferencesManager = default!;
    [Dependency] private IFileDialogManager _dialogManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private JobRequirementsManager _requirements = default!;
    [Dependency] private MarkingManager _markings = default!;
    [UISystemDependency] private readonly GuidebookSystem? _guide = default!;

    private DefaultWindow? _characterWindow;
    private CharacterSetupGui? _characterSetup;
    private HumanoidProfileEditor? _profileEditor;

    private void OpenCharacterSetup()
    {
        if (_characterWindow is { IsOpen: true })
        {
            _characterWindow.MoveToFront();
            return;
        }

        _profileEditor = new HumanoidProfileEditor(
            _preferencesManager,
            _cfg,
            EntityManager,
            _dialogManager,
            LogManager,
            _playerManager,
            _prototypeManager,
            _resourceCache,
            _requirements,
            _markings);

        if (_guide != null)
            _profileEditor.OnOpenGuidebook += _guide.OpenHelp;

        _profileEditor.Save += SaveCharacterProfile;
        _characterSetup = new CharacterSetupGui(_profileEditor);

        _characterSetup.CloseButton.OnPressed += _ =>
        {
            if (_profileEditor.Profile != null && _profileEditor.IsDirty)
            {
                OpenCharacterSavePanel();
                return;
            }

            CloseCharacterSetup();
        };

        _characterSetup.SelectCharacter += slot =>
        {
            _preferencesManager.SelectCharacter(slot);
            ReloadCharacterSetup();
        };

        _characterSetup.DeleteCharacter += slot =>
        {
            _preferencesManager.DeleteCharacter(slot);

            if (_profileEditor.CharacterSlot == slot)
                ReloadCharacterSetup();
            else
                _characterSetup.ReloadCharacterPickers();
        };

        _characterWindow = new DefaultWindow
        {
            Title = Loc.GetString("ui-escape-character"),
            Resizable = true,
            MinWidth = 1575,
            MinHeight = 550,
            SetWidth = 1050,
            SetHeight = 700,
        };

        _characterWindow.Contents.AddChild(_characterSetup);
        _characterWindow.OpenCentered();

        if (_preferencesManager.ServerDataLoaded)
        {
            _characterSetup.ReloadCharacterPickers();
            _profileEditor.SetProfile(
                (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter,
                _preferencesManager.Preferences?.SelectedCharacterIndex);
            _profileEditor.RefreshEscapeMenuMarkings();
        }
    }

    private void CloseCharacterSetup()
    {
        _characterWindow?.Dispose();
        _characterWindow = null;
        _characterSetup = null;
        _profileEditor = null;
    }

    private void SaveCharacterProfile()
    {
        if (_profileEditor?.Profile == null || _profileEditor.CharacterSlot == null)
            return;

        _preferencesManager.UpdateCharacter(_profileEditor.Profile, _profileEditor.CharacterSlot.Value);
        ReloadCharacterSetup();
    }

    private void ReloadCharacterSetup()
    {
        _characterSetup?.ReloadCharacterPickers();
        _profileEditor?.SetProfile(
            (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter,
            _preferencesManager.Preferences?.SelectedCharacterIndex);
        _profileEditor?.RefreshEscapeMenuMarkings();
    }

    private void OpenCharacterSavePanel()
    {
        var savePanel = new CharacterSetupGuiSavePanel();

        savePanel.SaveButton.OnPressed += _ =>
        {
            SaveCharacterProfile();
            savePanel.Close();
            CloseCharacterSetup();
        };

        savePanel.NoSaveButton.OnPressed += _ =>
        {
            savePanel.Close();
            CloseCharacterSetup();
        };

        savePanel.OpenCentered();
    }
}
