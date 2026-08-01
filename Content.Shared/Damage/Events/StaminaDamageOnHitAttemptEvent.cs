namespace Content.Shared.Damage.Events;

/// <summary>
/// Attempting to apply stamina damage on entity.
/// </summary>
[ByRefEvent]
public record struct StaminaDamageOnHitAttemptEvent(EntityUid? User = null, bool Cancelled = false); // <Onyx-MartialArtBlocked-edited>
