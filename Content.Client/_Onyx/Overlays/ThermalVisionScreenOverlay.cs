using System.Numerics;
using Content.Shared._Onyx.Overlays;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Overlays;

public sealed partial class ThermalVisionScreenOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "OnyxThermalVisionScreen";
    private readonly ShaderInstance _shader;

    public ThermalVisionComponent? Component;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ThermalVisionScreenOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        _shader.SetParameter("tint", new Vector3(0.3f, 0.3f, 0.3f));
        _shader.SetParameter("luminance_threshold", 2f);
        _shader.SetParameter("noise_amount", 0.5f);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || Component is not { Enabled: true } comp)
            return;

        var alpha = comp.PulseTime <= 0f ? 1f : Math.Clamp(comp.PulseRemaining / comp.PulseTime, 0f, 1f);
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldBounds, comp.Color.WithAlpha(alpha * 0.5f));
        args.WorldHandle.UseShader(null);
    }
}
