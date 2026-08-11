using Content.Shared._Onyx.ItemOffer;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Configuration;

namespace Content.Client._Onyx.ItemOffer;

public sealed partial class ItemOfferSystem : SharedItemOfferSystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_configuration, CCVars.ItemOfferCursorIndicator, OnIndicatorChanged, true);
    }

    public override void Shutdown()
    {
        _overlayManager.RemoveOverlay<ItemOfferIndicatorOverlay>();
        base.Shutdown();
    }

    private void OnIndicatorChanged(bool enabled)
    {
        if (enabled)
            _overlayManager.AddOverlay(new ItemOfferIndicatorOverlay(_input, _eye, _player, EntityManager, this));
        else
            _overlayManager.RemoveOverlay<ItemOfferIndicatorOverlay>();
    }
}
