using System.Linq;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared._Onyx.CCVar;
using Content.Shared.Speech.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using static Content.Client.Corvax.SponsorOnlyHelpers; // Corvax-Sponsors

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    public event Action<List<ProtoId<GuideEntryPrototype>>>? OnOpenGuidebook;

    private ColorSelectorSliders _rgbSkinColorSelector;
    private List<EmoteSoundsPrototype> _voices = new();
    private static readonly ProtoId<GuideEntryPrototype> DefaultSpeciesGuidebook = "Species";
    // <Onyx-HeightWidth>
    private const float WidthWeightExponent = 1.35f;
    private const float HeightWeightExponent = 0.65f;
    private bool _updatingDimensionControls;
    // </Onyx-HeightWidth>

    public void UpdateSpeciesGuidebookIcon()
    {
        SpeciesInfoButton.StyleClasses.Clear();

        var species = Profile?.Species;
        if (species is null)
            return;

        if (!_prototypeManager.Resolve<SpeciesPrototype>(species, out var speciesProto))
            return;

        // Don't display the info button if no guide entry is found
        if (!_prototypeManager.HasIndex<GuideEntryPrototype>(species))
            return;

        const string style = "SpeciesInfoDefault";
        SpeciesInfoButton.StyleIdentifier = style;
    }

    private void UpdateGenderControls()
    {
        if (Profile == null)
        {
            return;
        }

        PronounsButton.SelectId((int)Profile.Gender);
    }

    private void UpdateAgeEdit()
    {
        AgeEdit.Text = Profile?.Age.ToString() ?? "";
    }

    // <Onyx-HeightWidth>
    private void UpdateDimensionControls()
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        var height = species.ClampHeight(Profile.Height);
        var width = species.ClampWidth(Profile.Width);
        if (height != Profile.Height || width != Profile.Width)
            Profile = Profile.WithDimensions(height, width);

        _updatingDimensionControls = true;
        HeightSlider.Value = ToSlider(species.HeightScaleToCm(height), species.MinHeightCm, species.MaxHeightCm);
        WidthSlider.Value = ToSlider(species.WidthScaleToKg(width), species.MinWeightKg, species.MaxWeightKg);
        HeightEdit.Text = MathF.Round(species.HeightScaleToCm(height)).ToString("0");
        WidthEdit.Text = MathF.Round(species.WidthScaleToKg(width)).ToString("0");
        UpdateCalculatedWeightLabel(species);
        _updatingDimensionControls = false;
    }

    private static float ToSlider(float value, int min, int max)
    {
        return min == max ? 0f : Math.Clamp((value - min) / (max - min), 0f, 1f);
    }

    private void SetHeightSlider()
    {
        if (_updatingDimensionControls || Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        SetHeightCm((int)MathF.Round(MathHelper.Lerp(species.MinHeightCm, species.MaxHeightCm, HeightSlider.Value)));
    }

    private void SetWidthSlider()
    {
        if (_updatingDimensionControls || Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        SetWidthKg((int)MathF.Round(MathHelper.Lerp(species.MinWeightKg, species.MaxWeightKg, WidthSlider.Value)));
    }

    private void SetHeightCm(int value)
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        value = Math.Clamp(value, Math.Min(species.MinHeightCm, species.MaxHeightCm), Math.Max(species.MinHeightCm, species.MaxHeightCm));
        Profile = Profile.WithHeight(species.ClampHeight(species.HeightCmToScale(value)));
        UpdateDimensionControls();
        ReloadProfilePreview();
    }

    private void SetWidthKg(int value)
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        value = Math.Clamp(value, Math.Min(species.MinWeightKg, species.MaxWeightKg), Math.Max(species.MinWeightKg, species.MaxWeightKg));
        Profile = Profile.WithWidth(species.ClampWidth(species.WeightKgToScale(value)));
        UpdateDimensionControls();
        ReloadProfilePreview();
    }

    private void ResetHeight()
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        Profile = Profile.WithHeight(species.DefaultHeight);
        UpdateDimensionControls();
        ReloadProfilePreview();
    }

    private void ResetWidth()
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        Profile = Profile.WithWidth(species.DefaultWidth);
        UpdateDimensionControls();
        ReloadProfilePreview();
    }

    private void UpdateCalculatedWeightLabel(SpeciesPrototype species)
    {
        if (Profile == null)
            return;

        var heightRatio = species.HeightScaleToCm(Profile.Height) / Math.Max(species.DefaultHeightCm, 1);
        var widthRatio = species.WidthScaleToKg(Profile.Width) / Math.Max(species.DefaultWeightKg, 1);
        var weight = species.DefaultWeightKg
            * MathF.Pow(Math.Max(widthRatio, 0.01f), WidthWeightExponent)
            * MathF.Pow(Math.Max(heightRatio, 0.01f), HeightWeightExponent);
        CalculatedWeightLabel.Text = Loc.GetString("humanoid-profile-editor-calculated-weight-label", ("weight", MathF.Round(weight * 2f) / 2f));
    }
    // </Onyx-HeightWidth>

    private void UpdateSexControls()
    {
        if (Profile == null)
            return;

        SexButton.Clear();

        var sexes = new List<Sex>();

        // add species sex options, default to just none if we are in bizzaro world and have no species
        if (_prototypeManager.Resolve(Profile.Species, out var speciesProto))
        {
            foreach (var sex in speciesProto.Sexes)
            {
                sexes.Add(sex);
            }
        }
        else
        {
            sexes.Add(Sex.Unsexed);
        }

        // add button for each sex
        foreach (var sex in sexes)
        {
            SexButton.AddItem(Loc.GetString($"humanoid-profile-editor-sex-{sex.ToString().ToLower()}-text"), (int)sex);
        }

        if (sexes.Contains(Profile.Sex))
            SexButton.SelectId((int)Profile.Sex);
        else
            SexButton.SelectId((int)sexes[0]);
    }

    private void UpdateEyePickers()
    {
        if (Profile == null)
        {
            return;
        }

        _markingsModel.SetOrganEyeColor(Profile.Appearance.EyeColor);
        EyeColorPicker.SetData(Profile.Appearance.EyeColor);
    }

    private void UpdateVoiceControls()
    {
        if (Profile == null)
            return;

        VoiceButton.Clear();
        _voices.Clear();

        var speciesPrototype = _prototypeManager.Index(Profile.Species);
        var availableVoices = speciesPrototype.Voices;

        _voices.AddRange(availableVoices.Select(protoId => _prototypeManager.Index(protoId)));

        if (_voices.All(proto => Profile?.Voice != proto.ID))
            SetVoice(speciesPrototype.DefaultSoundsBySex[(int)Profile.Sex]);

        for (var i = 0; i < availableVoices.Count; i++)
        {
            var name = Loc.GetString(_voices[i].VoiceSelectorName);
            VoiceButton.AddItem(name, i);

            if (Profile?.Voice.Equals(_voices[i].ID) == true)
            {
                VoiceButton.SelectId(i);
            }
        }
    }

    private void UpdateSkinColor()
    {
        if (Profile == null)
            return;

        var skin = _prototypeManager.Index<SpeciesPrototype>(Profile.Species).SkinColoration;
        var strategy = _prototypeManager.Index(skin).Strategy;

        switch (strategy.InputType)
        {
            case SkinColorationStrategyInput.Unary:
                {
                    if (!Skin.Visible)
                    {
                        Skin.Visible = true;
                        RgbSkinColorContainer.Visible = false;
                    }

                    Skin.Value = strategy.ToUnary(Profile.Appearance.SkinColor);

                    break;
                }
            case SkinColorationStrategyInput.Color:
                {
                    if (!RgbSkinColorContainer.Visible)
                    {
                        Skin.Visible = false;
                        RgbSkinColorContainer.Visible = true;
                    }

                    _rgbSkinColorSelector.Color = strategy.ClosestSkinColor(Profile.Appearance.SkinColor);

                    break;
                }
        }
    }

    private void UpdateSpawnPriorityControls()
    {
        if (Profile == null)
        {
            return;
        }

        SpawnPriorityButton.SelectId((int)Profile.SpawnPriority);
    }

    /// <summary>
    /// Refreshes the species selector.
    /// </summary>
    public void RefreshSpecies() => RefreshSpeciesSelector(); // <Onyx-SpeciesSelector-edited>

    private void SetSpecies(string newSpecies)
    {
        Profile = Profile?.WithSpecies(newSpecies);
        OnSkinColorOnValueChanged(); // Species may have special color prefs, make sure to update it.
        UpdateMarkings(); // <Onyx-MarkingsTabs-edited>
        _markingsModel.ValidateMarkings();
        // In case there's job restrictions for the species
        RefreshJobs();
        // In case there's species restrictions for loadouts
        RefreshLoadouts();
        RefreshCybernetics(); // <Onyx-CyberneticsPersonalization>
        UpdateSexControls(); // update sex for new species
        UpdateVoiceControls();
        UpdateTTSVoicesControls(); // Corvax-TTS
        UpdateSpeciesGuidebookIcon();
        // <Onyx-HeightWidth>
        UpdateDimensionControls();
        // </Onyx-HeightWidth>
        ReloadPreview();
    }

    private void SetAge(int newAge)
    {
        Profile = Profile?.WithAge(newAge);
        ReloadPreview();
    }

    private void SetSex(Sex newSex)
    {
        Profile = Profile?.WithSex(newSex);
        // for convenience, default to most common gender when new sex is selected
        switch (newSex)
        {
            case Sex.Male:
                Profile = Profile?.WithGender(Gender.Male);
                break;
            case Sex.Female:
                Profile = Profile?.WithGender(Gender.Female);
                break;
            default:
                Profile = Profile?.WithGender(Gender.Epicene);
                break;
        }

        // this does the same as above but for voice
        if (_prototypeManager.TryIndex(Profile?.Species, out var prototype))
            SetVoice(prototype.DefaultSoundsBySex[(int)newSex]);

        UpdateGenderControls();
        UpdateVoiceControls();
        UpdateTTSVoicesControls(); // Corvax-TTS
        _markingsModel.SetOrganSexes(newSex);
        ReloadPreview();
    }

    private void SetVoice(ProtoId<EmoteSoundsPrototype> newVoice)
    {
        Profile = Profile?.WithVoice(newVoice);
        UpdateTTSVoicesControls(); // Corvax-TTS
        SetDirty();
    }

    private void SetGender(Gender newGender)
    {
        Profile = Profile?.WithGender(newGender);
        ReloadPreview();
    }

    private void SetSpawnPriority(SpawnPriorityPreference newSpawnPriority)
    {
        Profile = Profile?.WithSpawnPriorityPreference(newSpawnPriority);
        SetDirty();
    }

    private void OnSpeciesInfoButtonPressed(BaseButton.ButtonEventArgs args)
    {
        // TODO GUIDEBOOK
        // make the species guide book a field on the species prototype.
        // I.e., do what jobs/antags do.

        var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
        var species = Profile?.Species ?? HumanoidCharacterProfile.DefaultSpecies;
        var page = DefaultSpeciesGuidebook;
        if (_prototypeManager.HasIndex<GuideEntryPrototype>(species))
            page = new ProtoId<GuideEntryPrototype>(species.Id); // Gross. See above todo comment.

        if (_prototypeManager.Resolve(DefaultSpeciesGuidebook, out var guideRoot))
        {
            var dict = new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>();
            dict.Add(DefaultSpeciesGuidebook, guideRoot);
            //TODO: Don't close the guidebook if its already open, just go to the correct page
            guidebookController.OpenGuidebook(dict, includeChildren: true, selected: page);
        }
    }

    // <Onyx-Barks>
    private void SetBarkProto(string prototype)
    {
        Profile = Profile?.WithBarkProto(prototype);
        ReloadPreview();
        SetDirty();
    }

    private void SetBarkPitch(float pitch)
    {
        Profile = Profile?.WithBarkPitch(Math.Clamp(pitch, _cfgManager.GetCVar(ADTCCVars.BarksMinPitch), _cfgManager.GetCVar(ADTCCVars.BarksMaxPitch)));
        ReloadPreview();
        SetDirty();
    }

    private void SetBarkMinVariation(float variation)
    {
        Profile = Profile?.WithBarkMinVariation(Math.Clamp(variation, _cfgManager.GetCVar(ADTCCVars.BarksMinDelay), Profile.Bark.MaxVar));
        ReloadPreview();
        SetDirty();
    }

    private void SetBarkMaxVariation(float variation)
    {
        Profile = Profile?.WithBarkMaxVariation(Math.Clamp(variation, Profile.Bark.MinVar, _cfgManager.GetCVar(ADTCCVars.BarksMaxDelay)));
        ReloadPreview();
        SetDirty();
    }
    // </Onyx-Barks>

    private void OnSkinColorOnValueChanged()
    {
        if (Profile is null) return;

        var skin = _prototypeManager.Index<SpeciesPrototype>(Profile.Species).SkinColoration;
        var strategy = _prototypeManager.Index(skin).Strategy;

        switch (strategy.InputType)
        {
            case SkinColorationStrategyInput.Unary:
                {
                    if (!Skin.Visible)
                    {
                        Skin.Visible = true;
                        RgbSkinColorContainer.Visible = false;
                    }

                    var color = strategy.FromUnary(Skin.Value);

                    _markingsModel.SetOrganSkinColor(color);
                    Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithSkinColor(color));

                    break;
                }
            case SkinColorationStrategyInput.Color:
                {
                    if (!RgbSkinColorContainer.Visible)
                    {
                        Skin.Visible = false;
                        RgbSkinColorContainer.Visible = true;
                    }

                    var color = strategy.ClosestSkinColor(_rgbSkinColorSelector.Color);

                    _markingsModel.SetOrganSkinColor(color);
                    Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithSkinColor(color));

                    break;
                }
        }

        ReloadProfilePreview();
    }
}
