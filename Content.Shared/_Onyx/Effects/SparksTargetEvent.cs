// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
using Content.Shared._Onyx.AnimationData;

namespace Content.Shared._Onyx.Effects;

[Serializable, DataDefinition]
public sealed partial class DoSparksTargetEvent : BaseTargetEvent
{
    [DataField, AlwaysPushInheritance]
    public int MinSparks = 1;

    [DataField, AlwaysPushInheritance]
    public int MaxSparks = 3;

    [DataField, AlwaysPushInheritance]
    public int MinVelocity = 1;

    [DataField, AlwaysPushInheritance]
    public int MaxVelocity = 4;

    [DataField, AlwaysPushInheritance]
    public bool PlaySound = true;
}
