namespace Content.Shared._Onyx.Swimming.Events;

public sealed class OceanSwimmingSprintEvent : EntityEventArgs
{
    public bool IsSprinting;
    public float StaminaRegenMultiplier = 1f;
}
