using System.Collections.Generic;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;

namespace Content.Shared.Clothing.EntitySystems;

public sealed class ToggleClothingAttemptEvent(EntityUid user, EntityUid target, bool multiple) : CancellableEntityEventArgs
{
    public EntityUid User { get; } = user;
    public EntityUid Target { get; } = target;
    public bool Multiple { get; } = multiple;
}

public sealed class OnAttachedUnequipAttemptEvent(
    EntityUid toggleable,
    EntityUid attached,
    EntityUid unequiptarget,
    bool multiple) : CancellableEntityEventArgs
{
    public EntityUid Toggleable { get; } = toggleable;
    public EntityUid Attached { get; } = attached;
    public EntityUid UnEquipTarget { get; } = unequiptarget;
    public bool Multiple { get; } = multiple;
}

public sealed class OnToggleableUnequipAttemptEvent(
    EntityUid toggleable,
    EntityUid attached,
    EntityUid unequiptarget,
    bool multiple) : CancellableEntityEventArgs
{
    public EntityUid Toggleable { get; } = toggleable;
    public EntityUid Attached { get; } = attached;
    public EntityUid UnEquipTarget { get; } = unequiptarget;
    public bool Multiple { get; } = multiple;
}

[ByRefEvent]
public readonly record struct ToggledBackClothingFullUnequipAndInsertedEvent(
    EntityUid Toggleable,
    EntityUid Equipee,
    List<(EntityUid Part, string Slot)> Parts);

[Serializable, NetSerializable]
public sealed partial class AttachClothingDoAfterEvent : SimpleDoAfterEvent;
