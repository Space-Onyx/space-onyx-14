// Content taken from Wega (https://github.com/wega-team/ss14-wega), licensed under GNU GPL v3.
using Content.Client._Onyx.Lobby.UI;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void OnDescriptionChanged(CharacterDescriptionField field, string content)
    {
        if (Profile == null)
            return;

        Profile = field switch
        {
            CharacterDescriptionField.Appearance => Profile.WithFlavorText(content),
            CharacterDescriptionField.Character => Profile.WithCharacterFlavorText(content),
            CharacterDescriptionField.Ooc => Profile.WithOOCFlavorText(content),
            CharacterDescriptionField.Tags => Profile.WithTagsFlavorText(content),
            CharacterDescriptionField.Links => Profile.WithLinksFlavorText(content),
            _ => Profile,
        };

        _descriptionEditor?.UpdatePreview(Profile);
        SetDirty();
    }
}
