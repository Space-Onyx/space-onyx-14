// Content taken from Wega (https://github.com/wega-team/ss14-wega), licensed under GNU GPL v3.
using Content.Shared.Preferences;
using Robust.Shared.GameStates;

namespace Content.Shared.DetailExaminable;

public sealed partial class DetailExaminableComponent
{
    [DataField, AutoNetworkedField]
    public string CharacterContent = string.Empty;

    [DataField, AutoNetworkedField]
    public string OOCContent = string.Empty;

    [DataField, AutoNetworkedField]
    public string TagsContent = string.Empty;

    [DataField, AutoNetworkedField]
    public string LinksContent = string.Empty;
}

public static class DetailExaminableComponentExtensions
{
    public static void SetProfile(this DetailExaminableComponent component, HumanoidCharacterProfile profile)
    {
        component.Content = profile.FlavorText;
        component.CharacterContent = profile.CharacterFlavorText;
        component.OOCContent = profile.OOCFlavorText;
        component.TagsContent = profile.TagsFlavorText;
        component.LinksContent = profile.LinksFlavorText;
    }
}
