using System.Numerics;
using Content.Client.Resources;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChatSearchButton : ChatPopupButton<ChatSearchPopup>
{
    private static readonly Color ColorNormal = Color.FromHex("#7b7e9e");
    private static readonly Color ColorHovered = Color.FromHex("#9699bb");
    private static readonly Color ColorPressed = Color.FromHex("#789B8C");
    private readonly TextureRect? _textureRect;

    private const int SearchDropdownOffset = 120;

    public ChatSearchButton()
    {
        ToolTip = Loc.GetString("hud-chatbox-search-tooltip");

        var searchTexture = IoCManager.Resolve<IResourceCache>()
            .GetTexture("/Textures/_Onyx/Interface/Chat/search.png");

        AddChild(
            (_textureRect = new TextureRect
            {
                Texture = searchTexture,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center
            })
        );
    }

    protected override UIBox2 GetPopupPosition()
    {
        var globalPos = GlobalPosition;
        var (minX, minY) = Popup.MinSize;
        return UIBox2.FromDimensions(
            globalPos - new Vector2(SearchDropdownOffset, 0),
            new Vector2(Math.Max(minX, Popup.MinWidth), minY));
    }

    private void UpdateChildColors()
    {
        if (_textureRect == null) return;
        switch (DrawMode)
        {
            case DrawModeEnum.Normal:
                _textureRect.ModulateSelfOverride = ColorNormal;
                break;

            case DrawModeEnum.Pressed:
                _textureRect.ModulateSelfOverride = ColorPressed;
                break;

            case DrawModeEnum.Hover:
                _textureRect.ModulateSelfOverride = ColorHovered;
                break;

            case DrawModeEnum.Disabled:
                break;
        }
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateChildColors();
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        UpdateChildColors();
    }
}
