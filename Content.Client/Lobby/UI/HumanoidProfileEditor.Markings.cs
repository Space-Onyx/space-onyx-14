namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void UpdateMarkings()
    {
        if (Profile == null)
        {
            return;
        }

        // <Onyx-MarkingsTabs-edited>
        _markingsModel.OrganProfileData = _markingManager.GetProfileData(Profile.Species, Profile.Sex, Profile.Appearance.SkinColor, Profile.Appearance.EyeColor);
        _markingsModel.Markings = Profile.Appearance.Markings;
        _markingsModel.OrganData = _markingManager.GetMarkingData(Profile.Species);
        // </Onyx-MarkingsTabs-edited>
    }

    private void OnMarkingChange()
    {
        if (Profile is null || _settingProfile) // <Onyx-CharacterPersonalizationFix-edited>
            return;

        Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithMarkings(_markingsModel.Markings));
        ReloadProfilePreview();
        SetDirty();
    }
}
