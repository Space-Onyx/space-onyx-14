using Content.Shared.Inventory;
using Content.Goobstation.Shared.GrabIntent;

namespace Content.Shared._Onyx.Grab;

[RegisterComponent]
public sealed partial class GrabModifierComponent : Component
{
    [DataField]
    public GrabStage StartingGrabStage = GrabStage.Soft;

    [DataField]
    public float GrabEscapeModifier;

    [DataField]
    public float GrabEscapeMultiplier = 1f;

    [DataField]
    public float GrabMoveSpeedMultiplier = 1f;
}

[ByRefEvent]
public record struct GrabModifierEvent(EntityUid User, GrabStage Stage) : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.GLOVES;
    public GrabStage? NewStage;
    public float Multiplier = 1f;
    public float Modifier;
    public float SpeedMultiplier = 1f;
}

[ByRefEvent]
public record struct RaiseGrabModifierEvent(EntityUid User, GrabStage Stage, GrabStage? NewStage = null,
    float Multiplier = 1f, float Modifier = 0f, float SpeedMultiplier = 1f);
