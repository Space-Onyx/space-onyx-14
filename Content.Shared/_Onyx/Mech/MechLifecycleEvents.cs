namespace Content.Shared._Onyx.Mech;

/// <summary>
/// Raised on a prospective pilot before insertion into a mech.
/// </summary>
[ByRefEvent]
public record struct AttemptMechInsertEvent(EntityUid Mech)
{
    public bool Cancelled;
}

/// <summary>
/// Raised on a pilot after successful insertion into a mech.
/// </summary>
[ByRefEvent]
public record struct MechInsertedEvent(EntityUid Mech)
{
    public bool Cancelled;
}

/// <summary>
/// Raised on a pilot before explicit ejection from a mech.
/// </summary>
[ByRefEvent]
public record struct AttemptMechEjectEvent(EntityUid Mech, bool Forced)
{
    public bool Cancelled;
}

/// <summary>
/// Raised on a former pilot after removal from a mech and state cleanup.
/// </summary>
[ByRefEvent]
public readonly record struct MechEjectedEvent(EntityUid Mech);
