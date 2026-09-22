using System.Linq;
using System.Numerics;
using Content.Client._Onyx.Bark;
using Content.Client._Onyx.SpeechBarks;
using Content.Client.UserInterface.Controls;
using Content.Shared._Onyx.SpeechBarks;
using Content.Shared.CCVar;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<BarkPrototype> _barkList = new();
    private FancyWindow? _barkWindow;
    private bool _updatingSpeechRevealSpeed;

    private void InitializeBarks()
    {
        if (!_cfgManager.GetCVar(CCVars.BarksEnabled))
            return;

        BarksContainer.Visible = true;
        SpeechRevealSpeedContainer.Visible = _cfgManager.GetCVar(CCVars.SpeechBubbleRevealEnabled);
        _barkList = _prototypeManager
            .EnumeratePrototypes<BarkPrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => Loc.GetString(o.Name))
            .ToList();

        BarkProtoButton.OnPressed += _ => OpenBarkWindow();
        BarkPlayButton.OnPressed += _ => PlayPreviewBark();
        SpeechRevealSpeedSlider.MinValue = _cfgManager.GetCVar(CCVars.SpeechBubbleRevealMinSpeed);
        SpeechRevealSpeedSlider.MaxValue = _cfgManager.GetCVar(CCVars.SpeechBubbleRevealMaxSpeed);
        SpeechRevealSpeedSlider.OnValueChanged += _ =>
        {
            if (!_updatingSpeechRevealSpeed)
                SetSpeechBubbleRevealSpeed(SpeechRevealSpeedSlider.Value);
        };
        SpeechRevealSpeedEdit.OnTextChanged += args =>
        {
            if (!_updatingSpeechRevealSpeed && float.TryParse(args.Text, out var speed))
                SetSpeechBubbleRevealSpeed(speed, updateEdit: false);
        };
    }

    private void OpenBarkWindow()
    {
        if (Profile is null)
            return;

        if (_barkWindow != null)
        {
            _barkWindow.Close();
            _barkWindow = null;
        }
        
        var barkTab = new BarkTab();
        barkTab.SetSelectedBark(
            Profile.Bark.Proto,
            Profile.Bark.Pitch,
            Profile.Bark.MinVar,
            Profile.Bark.MaxVar);
        
        barkTab.OnBarkSelected += OnBarkSelected;
        barkTab.OnPitchChanged += OnBarkPitchChanged;
        barkTab.OnMinVarChanged += OnBarkMinVarChanged;
        barkTab.OnMaxVarChanged += OnBarkMaxVarChanged;

        _barkWindow = new FancyWindow
        {
            Title = Loc.GetString("humanoid-profile-editor-bark-window-title"),
            MinSize = new Vector2(750, 600),
        };
        _barkWindow.ContentsContainer.AddChild(barkTab);
        _barkWindow.OnClose += () =>
        {
            _barkWindow = null;
        };
        _barkWindow.OpenCentered();
    }

    private void OnBarkSelected(string barkId)
    {
        SetBarkProto(barkId);
        UpdateBarkButtonText();
        UpdateSaveButton();
    }

    private void OnBarkPitchChanged(float pitch)
    {
        SetBarkPitch(pitch);
        UpdateSaveButton();
    }

    private void OnBarkMinVarChanged(float minVar)
    {
        SetBarkMinVariation(minVar);
        UpdateSaveButton();
    }

    private void OnBarkMaxVarChanged(float maxVar)
    {
        SetBarkMaxVariation(maxVar);
        UpdateSaveButton();
    }

    private void UpdateBarkVoicesControls()
    {
        if (Profile is null)
            return;

        UpdateBarkButtonText();
        _updatingSpeechRevealSpeed = true;
        SpeechRevealSpeedSlider.Value = Profile.SpeechBubbleRevealSpeed;
        SpeechRevealSpeedEdit.Text = MathF.Round(Profile.SpeechBubbleRevealSpeed).ToString();
        _updatingSpeechRevealSpeed = false;
        // Обновляем окно барков если оно открыто
        if (_barkWindow != null && _barkWindow.ContentsContainer.ChildCount > 0)
        {
            var barkTab = _barkWindow.ContentsContainer.GetChild(0) as BarkTab;
            if (barkTab != null)
            {
                barkTab.SetSelectedBark(
                    Profile.Bark.Proto,
                    Profile.Bark.Pitch,
                    Profile.Bark.MinVar,
                    Profile.Bark.MaxVar);
            }
        }
    }

    private void UpdateBarkButtonText()
    {
        if (Profile is null)
            return;

        var bark = _barkList.FirstOrDefault(b => b.ID == Profile.Bark.Proto);
        if (bark != null)
        {
            BarkProtoButton.Text = Loc.GetString(bark.Name);
        }
        else
        {
            BarkProtoButton.Text = Loc.GetString("humanoid-profile-editor-bark-none");
        }
    }

    private void PlayPreviewBark()
    {
        if (Profile is null)
            return;

        _entManager.System<SpeechBarksSystem>().PlayDataPreview(
            Profile.Bark.Proto,
            Profile.Bark.Pitch,
            Profile.Bark.MinVar,
            Profile.Bark.MaxVar
        );
    }

    private void SetSpeechBubbleRevealSpeed(float speed, bool updateEdit = true)
    {
        if (Profile is null)
            return;

        speed = MathF.Round(Math.Clamp(
            speed,
            _cfgManager.GetCVar(CCVars.SpeechBubbleRevealMinSpeed),
            _cfgManager.GetCVar(CCVars.SpeechBubbleRevealMaxSpeed)));
        if (Profile.SpeechBubbleRevealSpeed == speed)
            return;

        Profile = Profile.WithSpeechBubbleRevealSpeed(speed);
        _updatingSpeechRevealSpeed = true;
        SpeechRevealSpeedSlider.Value = speed;
        if (updateEdit)
            SpeechRevealSpeedEdit.Text = speed.ToString();
        _updatingSpeechRevealSpeed = false;
        SetDirty();
    }

    private void SetBarkProto(string prototype)
    {
        Profile = Profile?.WithBarkProto(prototype);
        ReloadPreview();
        SetDirty();
    }

    private void SetBarkPitch(float pitch)
    {
        Profile = Profile?.WithBarkPitch(Math.Clamp(pitch, _cfgManager.GetCVar(CCVars.BarksMinPitch), _cfgManager.GetCVar(CCVars.BarksMaxPitch)));
        ReloadPreview();
        SetDirty();
    }

    private void SetBarkMinVariation(float variation)
    {
        Profile = Profile?.WithBarkMinVariation(Math.Clamp(variation, _cfgManager.GetCVar(CCVars.BarksMinDelay), Profile.Bark.MaxVar));
        ReloadPreview();
        SetDirty();
    }

    private void SetBarkMaxVariation(float variation)
    {
        Profile = Profile?.WithBarkMaxVariation(Math.Clamp(variation, Profile.Bark.MinVar, _cfgManager.GetCVar(CCVars.BarksMaxDelay)));
        ReloadPreview();
        SetDirty();
    }
}
