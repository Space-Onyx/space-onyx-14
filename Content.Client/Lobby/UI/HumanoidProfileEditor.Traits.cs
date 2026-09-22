using Content.Client._Onyx.Lobby.UI.Traits; // <Onyx-TraitsPersonalization>

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{

    /// <summary>
    /// Refreshes traits selector
    /// </summary>
    public void RefreshTraits()
    {
        // <Onyx-TraitsPersonalization-edited>
        if (Profile == null)
            return;

        TraitsList.RemoveAllChildren();
        _traitsControl ??= new TraitsPersonalizationControl();
        _traitsControl.Populate(Profile, profile =>
        {
            Profile = profile;
            SetDirty();
        });
        TraitsList.AddChild(_traitsControl);
        // </Onyx-TraitsPersonalization-edited>
    }
}
