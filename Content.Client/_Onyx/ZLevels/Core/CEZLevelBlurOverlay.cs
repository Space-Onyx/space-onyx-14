/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client.Viewport;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.ZLevels.Core;

public sealed partial class CEZLevelBlurOverlay : Overlay
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entity = default!;
    private readonly ShaderInstance? _blurShader;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ProtoId<ShaderPrototype> _zBlurShader = "CEZBlur";

    public CEZLevelBlurOverlay()
    {
        IoCManager.InjectDependencies(this);
        try
        {
            _blurShader = _proto.Index(_zBlurShader).InstanceUnique();
        }
        catch (Exception e)
        {
            Logger.GetSawmill("ce.zlevel.blur").Error($"Failed to load {_zBlurShader} shader; blur overlay disabled: {e}");
            _blurShader = null;
        }
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_blurShader is null)
            return false;

        if (args.Viewport.Eye is not ScalingViewport.ZEye zeye)
            return false;

        if (zeye.Depth >= 0)
            return false;

        if (args.MapId == MapId.Nullspace)
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null || _blurShader is not { } blurShader)
            return;

        var ambientColor = new Vector3(0, 0, 1);

        if (_entity.TryGetComponent<MapLightComponent>(args.MapUid, out var mapLight))
        {
            ambientColor = new Vector3(
                mapLight.AmbientLightColor.R,
                mapLight.AmbientLightColor.G,
                mapLight.AmbientLightColor.B);
        }

        blurShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        blurShader.SetParameter("BLUR_COLOR", ambientColor);
        blurShader.SetParameter("BLUR_RADIUS", CEClientZLevelsSystem.ZLevelBlurRadius);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(blurShader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}
