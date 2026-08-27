using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Bitrunning;

public sealed partial class BitrunningSpawnCheeseActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId PrototypeId = "FoodCheese";

    [DataField]
    public int Radius = 1;
}

public sealed partial class BitrunningLesserHealActionEvent : InstantActionEvent;

public sealed partial class BitrunningLightningActionEvent : EntityTargetActionEvent;
