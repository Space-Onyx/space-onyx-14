namespace Content.Shared._Onyx.Teleportation;

[ByRefEvent]
public record struct TeleportAttemptEvent(
    bool Predicted = true,
    string? Message = "teleport-blocked-message",
    bool Cancelled = false);
