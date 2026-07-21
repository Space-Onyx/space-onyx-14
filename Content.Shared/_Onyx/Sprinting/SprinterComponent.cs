using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Sprinting;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SprinterComponent : Component
{
    [AutoNetworkedField] public bool IsSprinting;
    [DataField, AutoNetworkedField] public bool CanSprint = true;
    [DataField, AutoNetworkedField] public float StaminaDrainRate = 9f;
    [DataField, AutoNetworkedField] public float StaminaRegenMultiplier = 0.75f;
    [DataField, AutoNetworkedField] public float SprintSpeedMultiplier = 1.45f;
    [DataField, AutoNetworkedField] public TimeSpan TimeBetweenSprints = TimeSpan.FromSeconds(3);
    [AutoNetworkedField] public TimeSpan LastSprint;
    [DataField] public EntProtoId SprintAnimation = "SprintAnimation";
    [DataField] public EntProtoId StepAnimation = "SmallSprintAnimation";
    [DataField] public SoundSpecifier SprintStartupSound = new SoundPathSpecifier("/Audio/_Onyx/Sprinting/sprint_puff.ogg");
    [DataField, AutoNetworkedField] public TimeSpan TimeBetweenSteps = TimeSpan.FromSeconds(0.6);
    [DataField] public float StaminaPenaltyOnShove = 25f;
    public TimeSpan LastStep;
}

[Serializable, NetSerializable]
public sealed class SprintToggleEvent(bool isSprinting) : EntityEventArgs
{
    public bool IsSprinting = isSprinting;
}

[Serializable, NetSerializable]
public sealed class SprintStartEvent : EntityEventArgs;

[ByRefEvent]
public sealed class SprintAttemptEvent : CancellableEntityEventArgs;
