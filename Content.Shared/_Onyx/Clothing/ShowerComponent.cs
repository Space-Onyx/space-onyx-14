using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Clothing;

[RegisterComponent]
public sealed partial class ShowerComponent : Component
{
    [DataField]
    public bool Enabled;

    [DataField]
    public string CleanerReagent = "Water";

    [DataField]
    public FixedPoint2 WashAmount = FixedPoint2.New(1);

    [DataField]
    public float WashInterval = 1f;

    [DataField]
    public float WashRange = 1.25f;

    [DataField]
    public SlotFlags TargetSlots = SlotFlags.WITHOUT_POCKET;

    public float WashAccumulator;
}

[Serializable, NetSerializable]
public enum ShowerVisuals : byte
{
    Enabled,
}

[Serializable, NetSerializable]
public enum ShowerVisualLayers : byte
{
    Water,
}
