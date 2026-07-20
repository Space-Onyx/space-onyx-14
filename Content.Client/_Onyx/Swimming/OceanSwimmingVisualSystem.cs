using Content.Shared._Onyx.Swimming.Components;
using Content.Shared._Onyx.Swimming.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;

namespace Content.Client._Onyx.Swimming;

public sealed partial class OceanSwimmingVisualSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> OceanSubmersionShader = "OnyxOceanSubmersion";
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedOceanSwimmingSystem _sharedOceanSystem = default!;
    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();
    private ShaderPrototype _shaderPrototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shaderPrototype = _prototypes.Index(OceanSubmersionShader);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sprite, out var xform))
        {
            var isOceanMap = xform.MapUid != null && HasComp<OceanMapComponent>(xform.MapUid.Value);

            var isSwimming = isOceanMap &&
                                xform.GridUid == null &&
                                !_sharedOceanSystem.ShouldIgnoreOceanSwimming(uid);

            if (isSwimming && TryComp<OceanMapComponent>(xform.MapUid!.Value, out var ocean))
            {
                if (TryApplyShader(uid, sprite, out var shader))
                {
                    shader.SetParameter("submersionDepth", Math.Clamp(ocean.SubmersionDepth, 0f, 1f));
                    shader.SetParameter("submergedAlpha", Math.Clamp(ocean.SubmergedAlpha, 0f, 1f));
                }
            }
            else
            {
                RemoveShader(uid, sprite);
            }
        }
    }
    private bool TryApplyShader(EntityUid uid, SpriteComponent sprite, out ShaderInstance shader)
    {
        if (_shaders.TryGetValue(uid, out shader!))
        {
            if (sprite.PostShader == null)
            {
                sprite.PostShader = shader;
                return true;
            }

            if (sprite.PostShader == shader)
                return true;

            _shaders.Remove(uid);
            shader = default!;
            return false;
        }

        if (sprite.PostShader != null)
        {
            shader = default!;
            return false;
        }

        shader = _shaderPrototype.InstanceUnique();
        _shaders.Add(uid, shader);
        sprite.PostShader = shader;

        return true;
    }

    private void RemoveShader(EntityUid uid, SpriteComponent sprite)
    {
        if (!_shaders.Remove(uid, out var shader))
            return;

        if (sprite.PostShader == shader)
            sprite.PostShader = null;
    }
}