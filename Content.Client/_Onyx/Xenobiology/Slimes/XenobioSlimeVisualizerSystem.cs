using Content.Client.DamageState;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Xenobiology.Slimes;

public sealed partial class XenobioSlimeVisualizerSystem : VisualizerSystem<XenobioSlimeComponent>
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    protected override void OnAppearanceChange(EntityUid uid,
        XenobioSlimeComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !SpriteSystem.LayerMapTryGet((uid, args.Sprite), DamageStateVisualLayers.Base, out var layer, false) ||
            !AppearanceSystem.TryGetData<Color>(uid, XenobioSlimeVisuals.Color, out var color, args.Component))
            return;

        SpriteSystem.LayerSetColor((uid, args.Sprite), layer, color);

        if (!AppearanceSystem.TryGetData<string?>(uid, XenobioSlimeVisuals.Shader, out var shader, args.Component) ||
            string.IsNullOrEmpty(shader))
        {
            args.Sprite.LayerSetShader(layer, null, null);
            args.Sprite.GetScreenTexture = false;
            args.Sprite.RaiseShaderEvent = false;
            return;
        }

        if (!_prototypes.TryIndex<ShaderPrototype>(shader, out var prototype))
        {
            args.Sprite.LayerSetShader(layer, null, null);
            args.Sprite.GetScreenTexture = false;
            args.Sprite.RaiseShaderEvent = false;
            return;
        }

        args.Sprite.LayerSetShader(layer, prototype.InstanceUnique());
        args.Sprite.GetScreenTexture = true;
        args.Sprite.RaiseShaderEvent = true;
    }
}
