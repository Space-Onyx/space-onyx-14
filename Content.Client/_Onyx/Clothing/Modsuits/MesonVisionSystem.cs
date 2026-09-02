using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._Onyx.Clothing.Modsuits;

public sealed partial class MesonVisionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    private MesonVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MesonVisionComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MesonVisionComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<MesonVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<MesonVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        _overlay = new MesonVisionOverlay();
    }

    private void OnComponentInit(Entity<MesonVisionComponent> ent, ref ComponentInit args)
    {
        if (ent.Owner == _player.LocalEntity)
            AddOverlay();
    }

    private void OnComponentShutdown(Entity<MesonVisionComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner == _player.LocalEntity)
            RemoveOverlay();
    }

    private void OnPlayerAttached(Entity<MesonVisionComponent> ent, ref LocalPlayerAttachedEvent args) => AddOverlay();
    private void OnPlayerDetached(Entity<MesonVisionComponent> ent, ref LocalPlayerDetachedEvent args) => RemoveOverlay();

    private void AddOverlay()
    {
        if (!_overlayManager.HasOverlay<MesonVisionOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    private void RemoveOverlay() => _overlayManager.RemoveOverlay(_overlay);
}
