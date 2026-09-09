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
        TraitsList.RemoveAllChildren();
        var traits = new TraitsPersonalizationControl();
        traits.Populate(Profile, profile =>
        {
            Profile = profile;
            SetDirty();
        });
        TraitsList.AddChild(traits);
        // </Onyx-TraitsPersonalization-edited>
    }
}
