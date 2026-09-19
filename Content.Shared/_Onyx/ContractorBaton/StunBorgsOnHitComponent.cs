// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using Content.Shared.Item.ItemToggle;

namespace Content.Shared._Onyx.ContractorBaton;

[RegisterComponent]
public sealed partial class StunBorgsOnHitComponent : Component
{
    /// <summary>
    /// Paralyze duration applied to borgs on hit while the baton is activated.
    /// </summary>
    [DataField]
    public TimeSpan ParalyzeDuration = TimeSpan.FromSeconds(5);
}
