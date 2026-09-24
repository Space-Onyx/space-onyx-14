// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Stunnable;

[RegisterComponent, NetworkedComponent]
public sealed partial class OvertimeStaminaDamageComponent : Component
{
    /// <summary>
    /// Delay between overtime ticks in seconds.
    /// </summary>
    [DataField]
    public float Delay = 1f;

    /// <summary>
    /// Time left until the next overtime tick. Set by the system.
    /// </summary>
    public float Timer = 1f;

    /// <summary>
    /// Total stamina damage to deal over time.
    /// </summary>
    [DataField]
    public float Amount = 10f;

    /// <summary>
    /// Remaining stamina damage. Set by the system.
    /// </summary>
    public float Damage = 10f;

    /// <summary>
    /// Divisor controlling how the total amount is split across ticks.
    /// </summary>
    [DataField]
    public float Delta = 5f;
}
