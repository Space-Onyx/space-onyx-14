using Content.Shared.Light;
using Content.Shared.PDA;
using Robust.Client.GameObjects;
using Robust.Shared.Utility; // <Onyx-PdaScreenVisuals>

namespace Content.Client.PDA;

public sealed partial class PdaVisualizerSystem : VisualizerSystem<PdaVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, PdaVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // <Onyx-PdaScreenVisuals-edited>
        var hasScreenLayer = SpriteSystem.LayerMapTryGet((uid, args.Sprite), PdaVisualLayers.Screen, out _, false);
        if (!hasScreenLayer &&
            AppearanceSystem.TryGetData<string>(uid, PdaVisuals.PdaType, out var pdaType, args.Component))
        {
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), PdaVisualLayers.Base, pdaType);
        }

        if (hasScreenLayer && TryComp<PdaBorderColorComponent>(uid, out var colors))
        {
            SpriteSystem.LayerSetColor((uid, args.Sprite), PdaVisualLayers.Base, Color.FromHex(colors.BorderColor, Color.White));
            SetAccent((uid, args.Sprite), PdaVisualLayers.AccentV, colors.AccentVColor);
            SetAccent((uid, args.Sprite), PdaVisualLayers.AccentH, colors.AccentHColor);
        }

        if (hasScreenLayer &&
            AppearanceSystem.TryGetData<SpriteSpecifier>(uid, PdaVisuals.ScreenState, out var screen, args.Component) &&
            !Equals(comp.LastScreen, screen))
        {
            SpriteSystem.LayerSetSprite((uid, args.Sprite), PdaVisualLayers.Screen, screen);
            comp.LastScreen = screen;
        }
        // </Onyx-PdaScreenVisuals-edited>

        if (AppearanceSystem.TryGetData<bool>(uid, UnpoweredFlashlightVisuals.LightOn, out var isFlashlightOn, args.Component))
            SpriteSystem.LayerSetVisible((uid, args.Sprite), PdaVisualLayers.Flashlight, isFlashlightOn);

        if (AppearanceSystem.TryGetData<bool>(uid, PdaVisuals.IdCardInserted, out var isCardInserted, args.Component))
            SpriteSystem.LayerSetVisible((uid, args.Sprite), PdaVisualLayers.IdLight, isCardInserted);
    }

    // <Onyx-PdaScreenVisuals>
    private void SetAccent(Entity<SpriteComponent?> sprite, PdaVisualLayers layer, string? color)
    {
        if (!SpriteSystem.LayerMapTryGet(sprite, layer, out _, false))
            return;

        SpriteSystem.LayerSetVisible(sprite, layer, color != null);
        if (color != null)
            SpriteSystem.LayerSetColor(sprite, layer, Color.FromHex(color, Color.White));
    }
    // </Onyx-PdaScreenVisuals>

    public enum PdaVisualLayers : byte
    {
        Base,
        Buttons, // <Onyx-PdaScreenVisuals>
        AccentV, // <Onyx-PdaScreenVisuals>
        AccentH, // <Onyx-PdaScreenVisuals>
        Screen, // <Onyx-PdaScreenVisuals>
        Flashlight,
        IdLight
    }
}
