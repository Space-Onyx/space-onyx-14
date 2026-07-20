using Content.Shared._Onyx.Footprints;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Client._Onyx.Footprints;

public sealed partial class FootprintSystem : EntitySystem
{
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
            if (!sprite.LayerExists(i, false))
                sprite.AddBlankLayer(i);

            var print = footprint.Footprints[i];
            sprite.LayerSetOffset(i, print.Offset);
            sprite.LayerSetRotation(i, print.Rotation);
            sprite.LayerSetColor(i, print.Color);
            sprite.LayerSetSprite(i, new SpriteSpecifier.Rsi(new("/Textures/_Onyx/Effects/footprint.rsi"), print.State));
        }

        for (var i = sprite.AllLayers.Count() - 1; i >= footprint.Footprints.Count; i--)
            sprite.RemoveLayer(i);
    }
}
