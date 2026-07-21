using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.GrabIntent;

[Serializable, NetSerializable]
public enum GrabStage : byte
{
    No,
    Soft,
    Hard,
    Suffocate,
}

[ByRefEvent]
public record struct GrabAttemptEvent(EntityUid Puller)
{
    public bool Grabbed;
}

[ByRefEvent]
public record struct GrabReleaseAttemptEvent(EntityUid? User)
{
    public bool Released = true;
}
