using Content.Client._Onyx.Lobby.UI; // <Onyx-CharacterDescriptions>

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private bool _allowFlavorText;

    private CharacterDescriptionEditor? _descriptionEditor; // <Onyx-CharacterDescriptions-edited>

    /// <summary>
    /// Refreshes the flavor text editor status.
    /// </summary>
    public void RefreshFlavorText()
    {
        if (_allowFlavorText)
        {
            if (_descriptionEditor != null) // <Onyx-CharacterDescriptions-edited>
                return;

            // <Onyx-CharacterDescriptions-edited>
            _descriptionEditor = new CharacterDescriptionEditor();
            TabContainer.AddChild(_descriptionEditor);
            TabContainer.SetTabTitle(TabContainer.ChildCount - 1, Loc.GetString("humanoid-profile-editor-flavortext-tab"));
            _descriptionEditor.DescriptionChanged += OnDescriptionChanged;
            _descriptionEditor.SetProfile(Profile);
            // </Onyx-CharacterDescriptions-edited>
        }
        else
        {
            if (_descriptionEditor == null) // <Onyx-CharacterDescriptions-edited>
                return;

            // <Onyx-CharacterDescriptions-edited>
            TabContainer.RemoveChild(_descriptionEditor);
            _descriptionEditor.DescriptionChanged -= OnDescriptionChanged;
            _descriptionEditor.Dispose();
            _descriptionEditor = null;
            // </Onyx-CharacterDescriptions-edited>
        }
    }

    private void UpdateFlavorTextEdit()
    {
        _descriptionEditor?.SetProfile(Profile); // <Onyx-CharacterDescriptions-edited>
    }
}
