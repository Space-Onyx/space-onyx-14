// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
using Content.Shared._Onyx.AnimationData;

namespace Content.Server._Onyx.Trigger;

[RegisterComponent]
public sealed partial class ExecuteTargetEventsOnTriggerComponent : Component
{
    [DataField]
    public List<BaseTargetEvent> Events = new();
}
