using Content.Shared.Radio.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Clothing.EntitySystems;

public abstract partial class SharedChameleonClothingSystem
{
    private void UpdateHeadsetColor(EntityUid uid, EntityPrototype proto)
    {
        if (TryComp<HeadsetComponent>(uid, out var headset) &&
            proto.TryComp(out HeadsetComponent? otherHeadset, Factory))
        {
            headset.Color = otherHeadset.Color;
        }
    }
}
