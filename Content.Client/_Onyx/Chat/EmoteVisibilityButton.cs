using System.Numerics;
using Content.Client.Resources;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class EmoteVisibilityButton : ChatPopupButton<EmoteVisibilityPopup>
{
    public EmoteVisibilityButton()
    {
        ToolTip = Loc.GetString("hud-chatbox-emote-visibility-tooltip");

        AddChild(new TextureRect
        {
            Texture = IoCManager.Resolve<IResourceCache>().GetTexture("/Textures/Interface/Actions/eyeopen.png"),
            SetSize = new Vector2(20),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        });
    }

    protected override UIBox2 GetPopupPosition()
    {
        var size = new Vector2(Math.Max(Popup.MinSize.X, Popup.MinWidth), Popup.MinSize.Y);
        return UIBox2.FromDimensions(new Vector2(GlobalPosition.X - size.X + Width, GlobalPosition.Y), size);
    }
}
