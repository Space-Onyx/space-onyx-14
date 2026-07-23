using Content.Shared._Onyx.Footprints;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Client._Onyx.Footprints;

public sealed partial class FootprintSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FootprintComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FootprintComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<FootprintComponent> ent, ref ComponentStartup args)
    {
        UpdateSprite(ent, ent);
    }

    private void OnState(Entity<FootprintComponent> ent, ref AfterAutoHandleStateEvent args)
        => UpdateSprite(ent, ent);

    private void UpdateSprite(EntityUid uid, FootprintComponent footprint)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        for (var i = 0; i < footprint.Footprints.Count; i++)
        {
            if (!_sprite.LayerExists((uid, sprite), i))
                _sprite.AddBlankLayer((uid, sprite), i);

            var print = footprint.Footprints[i];
            _sprite.LayerSetOffset((uid, sprite), i, print.Offset);
            _sprite.LayerSetRotation((uid, sprite), i, print.Rotation);
            _sprite.LayerSetColor((uid, sprite), i, print.Color);
            _sprite.LayerSetSprite((uid, sprite), i, new SpriteSpecifier.Rsi(new("/Textures/_Onyx/Effects/footprint.rsi"), print.State));
        }

        for (var i = sprite.AllLayers.Count() - 1; i >= footprint.Footprints.Count; i--)
            _sprite.RemoveLayer((uid, sprite), i);
    }
}
