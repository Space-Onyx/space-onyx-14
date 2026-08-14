using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Onyx.Lobby.UI.Loadouts;

[Virtual]
public class LoadoutRoleSelector : OptionButton
{
    private const string IconPadding = "          ";
    private readonly TextureRect _selectedIcon;
    private Texture? _addingIcon;

    public LoadoutRoleSelector()
    {
        AddStyleClass(StyleClass.ButtonSquare);
        Prefix = IconPadding;
        PrefixMargin = false;
        _selectedIcon = new TextureRect
        {
            SetSize = new Vector2(24),
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            MouseFilter = MouseFilterMode.Ignore,
        };
        AddChild(_selectedIcon);
    }

    public void SetSelectedIcon(Texture? icon)
    {
        _selectedIcon.Texture = icon;
    }

    public void SetSelectedIconVisible(bool visible)
    {
        _selectedIcon.Visible = visible;
        Prefix = visible ? IconPadding : string.Empty;
    }

    public void AddJob(string name, Texture icon, int id)
    {
        _addingIcon = icon;
        AddItem(name, id);
        _addingIcon = null;
    }

    public override void ButtonOverride(Button button)
    {
        base.ButtonOverride(button);
        button.AddStyleClass(StyleClass.ButtonSquare);
        button.Text = IconPadding + button.Text;
        button.AddChild(new TextureRect
        {
            Texture = _addingIcon,
            SetSize = new Vector2(22),
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(7, 0, 0, 0),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            MouseFilter = MouseFilterMode.Ignore,
        });
    }
}
