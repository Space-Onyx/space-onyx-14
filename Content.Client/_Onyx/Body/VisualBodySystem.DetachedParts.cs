using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.Body;

public sealed partial class VisualBodySystem
{
    private void ClearDetachedBodyPartVisuals(EntityUid root)
    {
        foreach (var layer in Enum.GetValues<HumanoidVisualLayers>())
        {
            if (_sprite.LayerMapTryGet(root, layer, out var index, false))
                _sprite.LayerSetRsiState(root, index, RSI.StateId.Invalid);
        }
    }
}
