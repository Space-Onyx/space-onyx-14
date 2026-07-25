using Content.Shared._Onyx.TimedDespawn;

namespace Content.Server._Onyx.TimedDespawn;

public sealed partial class FadingTimedDespawnSystem : SharedFadingTimedDespawnSystem
{
    protected override bool CanDelete(EntityUid uid) => true;
}
