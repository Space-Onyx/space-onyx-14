using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.ItemOffer;

public sealed class ItemOfferIndicatorOverlay : Overlay
{
    private readonly IInputManager _input;
    private readonly IEyeManager _eye;
    private readonly IPlayerManager _player;
    private readonly ItemOfferSystem _system;
    private readonly Texture _indicator;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public ItemOfferIndicatorOverlay(
        IInputManager input,
        IEyeManager eye,
        IPlayerManager player,
        IEntityManager entityManager,
        ItemOfferSystem system)
    {
        _input = input;
        _eye = eye;
        _player = player;
        _system = system;
        var sprites = entityManager.System<SpriteSystem>();
        _indicator = sprites.Frame0(new SpriteSpecifier.Rsi(
            new ResPath("/Textures/_Onyx/Interface/Misc/give_item.rsi"), "give_item"));
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _system.IsInOfferMode(_player.LocalEntity) && base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var mouse = _input.MouseScreenPosition;
        if (_eye.PixelToMap(mouse).MapId != args.MapId)
            return;

        var uiScale = (args.ViewportControl as Control)?.UIScale ?? 1f;
        DrawIndicator(args.ScreenHandle, mouse.Position, Math.Min(1.25f, uiScale) * 0.6f);
    }

    private void DrawIndicator(DrawingHandleScreen handle, Vector2 center, float scale)
    {
        var size = _indicator.Size * scale;
        var outlineSize = size + new Vector2(7f);
        handle.DrawTextureRect(_indicator,
            UIBox2.FromDimensions(center - size * 0.5f, size), Color.Black.WithAlpha(0.5f));
        handle.DrawTextureRect(_indicator,
            UIBox2.FromDimensions(center - outlineSize * 0.5f, outlineSize), Color.White.WithAlpha(0.3f));
    }
}
