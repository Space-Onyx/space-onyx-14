using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Onyx.Lobby.UI.Loadouts;

[Virtual]
public class LoadoutRoleSelector : OptionButton
{
    private const string IconPadding = "          ";
    private readonly TextureRect _selectedIcon;
    private Texture? _addingIcon;
    private PanelContainer? _compactOverlay;

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

    public void SetCompactDisplay(bool locked)
    {
        Prefix = string.Empty;
        HideTriangle = true;
        _selectedIcon.Visible = false;
        HideSelectedLabel(this);
        _compactOverlay?.Dispose();
        _compactOverlay = new PanelContainer
        {
            MouseFilter = MouseFilterMode.Ignore,
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex(locked ? "#24201C" : "#202832"),
                BorderColor = Color.FromHex(locked ? "#624D35" : "#3F5264"),
                BorderThickness = new Thickness(1),
            },
            Children =
            {
                new Label
                {
                    Text = "▼",
                    Align = Label.AlignMode.Center,
                    VerticalAlignment = VAlignment.Center,
                    MouseFilter = MouseFilterMode.Ignore,
                    FontColorOverride = Color.FromHex(locked ? "#B6A992" : "#A9D4FF"),
                },
            },
        };
        AddChild(_compactOverlay);
    }

    private static bool HideSelectedLabel(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is Label)
            {
                child.Visible = false;
                return true;
            }

            if (HideSelectedLabel(child))
                return true;
        }

        return false;
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
