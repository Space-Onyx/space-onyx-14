using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Clothing.Modsuits;

public sealed partial class RaveOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "OnyxRave";

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly ShaderInstance _shader;
    private float _pulseSpeed;
    private float _intensity;
    private float _grain;
    private float _distortion;
    private Color _baseColor;
    private Color _secondaryColor;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public RaveOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypes.Index(Shader).InstanceUnique();
    }

    public void UpdateParameters(RaveOverlayComponent component)
    {
        _baseColor = component.BaseColor;
        _secondaryColor = component.SecondaryColor;
        _pulseSpeed = component.PulseSpeed;
        _intensity = component.Intensity;
        _grain = component.GrainStrength;
        _distortion = component.Distortion;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;
        var time = (float) _timing.RealTime.TotalSeconds;
        var pulse = 0.7f + 0.3f * MathF.Sin(time * _pulseSpeed * MathF.PI * 2);
        var color = Color.InterpolateBetween(_baseColor, _secondaryColor, (MathF.Sin(time * 0.2f) + 1f) / 2f);
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("time", time);
        _shader.SetParameter("color_r", color.R);
        _shader.SetParameter("color_g", color.G);
        _shader.SetParameter("color_b", color.B);
        _shader.SetParameter("intensity", _intensity * (0.8f + 0.4f * pulse));
        _shader.SetParameter("grain", _grain);
        _shader.SetParameter("distortion", _distortion);
        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
        args.WorldHandle.UseShader(null);
    }
}
