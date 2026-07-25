namespace Content.Server._Onyx.Singularity;

[ByRefEvent]
public record struct ContainmentFieldThrowEvent(EntityUid Field, bool Cancelled = false);
