// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Maths;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private bool _updatingDimensionControls;
    private DateTime _lastDescriptionPreviewUpdate = DateTime.MinValue;

    private void InitializeDimensions()
    {
        HeightEdit.OnTextChanged += args =>
        {
            if (!_updatingDimensionControls && int.TryParse(args.Text, out var value))
                SetHeightCm(value, updateText: false);
        };
        WidthEdit.OnTextChanged += args =>
        {
            if (!_updatingDimensionControls && int.TryParse(args.Text, out var value))
                SetWidthCm(value, updateText: false);
        };
        HeightSlider.OnValueChanged += _ => SetHeightSlider();
        WidthSlider.OnValueChanged += _ => SetWidthSlider();
        HeightReset.OnPressed += _ => ResetHeight();
        WidthReset.OnPressed += _ => ResetWidth();
    }

    private void UpdateDimensionControls(bool updateText = true)
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        var height = species.ClampHeight(Profile.Height);
        var width = species.ClampWidth(Profile.Width);
        if (height != Profile.Height || width != Profile.Width)
            Profile = Profile.WithDimensions(height, width);

        _updatingDimensionControls = true;
        HeightSlider.Value = ToSlider(species.HeightScaleToCm(height), species.MinHeightCm, species.MaxHeightCm);
        WidthSlider.Value = ToSlider(species.WidthScaleToCm(width), species.MinWidthCm, species.MaxWidthCm);
        if (updateText)
        {
            HeightEdit.Text = MathF.Round(species.HeightScaleToCm(height)).ToString("0");
            WidthEdit.Text = MathF.Round(species.WidthScaleToCm(width)).ToString("0");
        }
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

        SetWidthCm((int)MathF.Round(MathHelper.Lerp(species.MinWidthCm, species.MaxWidthCm, WidthSlider.Value)));
    }

    private void SetHeightCm(int value, bool updateText = true)
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        value = Math.Clamp(value, Math.Min(species.MinHeightCm, species.MaxHeightCm), Math.Max(species.MinHeightCm, species.MaxHeightCm));
        Profile = Profile.WithHeight(species.ClampHeight(species.HeightCmToScale(value)));
        UpdateDimensionControls(updateText);
        ReloadProfilePreview();
        RefreshDescriptionPreviewThrottled();
    }

    private void SetWidthCm(int value, bool updateText = true)
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        value = Math.Clamp(value, Math.Min(species.MinWidthCm, species.MaxWidthCm), Math.Max(species.MinWidthCm, species.MaxWidthCm));
        Profile = Profile.WithWidth(species.ClampWidth(species.WidthCmToScale(value)));
        UpdateDimensionControls(updateText);
        ReloadProfilePreview();
        RefreshDescriptionPreviewThrottled();
    }

    private void ResetHeight()
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        Profile = Profile.WithHeight(species.DefaultHeight);
        UpdateDimensionControls();
        ReloadProfilePreview();
        _descriptionEditor?.UpdatePreview(Profile, true);
    }

    private void ResetWidth()
    {
        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        Profile = Profile.WithWidth(species.DefaultWidth);
        UpdateDimensionControls();
        ReloadProfilePreview();
        _descriptionEditor?.UpdatePreview(Profile, true);
    }

    private void RefreshDescriptionPreviewThrottled()
    {
        var now = DateTime.UtcNow;
        if (now - _lastDescriptionPreviewUpdate < TimeSpan.FromMilliseconds(250))
            return;

        _lastDescriptionPreviewUpdate = now;
        _descriptionEditor?.UpdatePreview(Profile, true);
    }

    private void UpdateCalculatedWeightLabel(SpeciesPrototype species)
    {
        if (Profile == null)
            return;

        var weight = species.GetEstimatedWeightKg(Profile.Height, Profile.Width);
        CalculatedWeightLabel.Text = Loc.GetString("humanoid-profile-editor-calculated-weight-label", ("weight", MathF.Round(weight * 2f) / 2f));
    }
}
