using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical;

[Serializable, NetSerializable]
public sealed partial class HealingDoAfterEvent : SimpleDoAfterEvent
{
    // Onyx-WoundSystem-edited: explicit part survives prediction and is revalidated after the delay.
    public readonly NetEntity? RequestedPart;

    public HealingDoAfterEvent(NetEntity? requestedPart = null)
    {
        RequestedPart = requestedPart;
    }
}
