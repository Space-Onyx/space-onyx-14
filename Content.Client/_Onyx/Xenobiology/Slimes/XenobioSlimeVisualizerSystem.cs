using Content.Client.DamageState;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Xenobiology.Slimes;

public sealed partial class XenobioSlimeVisualizerSystem : VisualizerSystem<XenobioSlimeComponent>
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobioSlimeComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        foreach (var shader in _shaders.Values)
            shader.Dispose();
        _shaders.Clear();
        base.Shutdown();
    }

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
            ClearShader(uid, args.Sprite, layer);
            args.Sprite.GetScreenTexture = false;
            args.Sprite.RaiseShaderEvent = false;
            return;
        }

        if (!_prototypes.TryIndex<ShaderPrototype>(shader, out var prototype))
        {
            ClearShader(uid, args.Sprite, layer);
            args.Sprite.GetScreenTexture = false;
            args.Sprite.RaiseShaderEvent = false;
            return;
        }

        if (_shaders.TryGetValue(uid, out var current) &&
            args.Sprite[layer] is SpriteComponent.Layer spriteLayer &&
            spriteLayer.Shader == current &&
            spriteLayer.ShaderPrototype == shader)
        {
            args.Sprite.GetScreenTexture = false;
            args.Sprite.RaiseShaderEvent = false;
            return;
        }

        ClearShader(uid, args.Sprite, layer);
        var instance = prototype.InstanceUnique();
        _shaders[uid] = instance;
        args.Sprite.LayerSetShader(layer, instance, shader);
        args.Sprite.GetScreenTexture = false;
        args.Sprite.RaiseShaderEvent = false;
    }

    private void OnShutdown(Entity<XenobioSlimeComponent> ent, ref ComponentShutdown args)
    {
        if (_shaders.Remove(ent.Owner, out var shader))
            shader.Dispose();
    }

    private void ClearShader(EntityUid uid, SpriteComponent sprite, int layer)
    {
        sprite.LayerSetShader(layer, null, null);
        if (_shaders.Remove(uid, out var shader))
            shader.Dispose();
    }
}
