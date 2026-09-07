using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Body.Part;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Clothing;

[RegisterComponent]
public sealed partial class ClothingDirtWasherComponent : Component
{
    [DataField]
    public string CleanerReagent = "Water";

    [DataField]
    public FixedPoint2 Amount = FixedPoint2.New(1);

    [DataField]
    public TimeSpan WashTime = TimeSpan.FromSeconds(2);
}

[Serializable, NetSerializable]
public sealed partial class WashClothingDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class WashBodyDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public BodyWashTarget WashTarget;

    public WashBodyDoAfterEvent(BodyWashTarget target)
    {
        WashTarget = target;
    }
}

[Serializable, NetSerializable]
public enum BodyWashTarget : byte
{
    Hands,
    Face,
}
