// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.ReverseBearTrap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReverseBearTrapComponent : Component
{
    /// <summary>
    /// Countdown duration in seconds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CountdownDuration;

    /// <summary>
    /// Current wearer of the trap.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? Wearer;

    /// <summary>
    /// Whether the trap countdown is ticking.
    /// </summary>
    [AutoNetworkedField]
    public bool Ticking;

    /// <summary>
    /// When the trap was armed.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan ActivateTime;

    /// <summary>
    /// Current escape chance in percent.
    /// </summary>
    [AutoNetworkedField]
    public float CurrentEscapeChance;

    /// <summary>
    /// Whether the wearer is currently struggling.
    /// </summary>
    [AutoNetworkedField]
    public bool Struggling;

    [AutoNetworkedField]
    public EntityUid? LoopSoundStream { get; set; }

    /// <summary>
    /// Looping tick sound while armed.
    /// </summary>
    [DataField]
    public SoundSpecifier LoopSound { get; set; } = new SoundPathSpecifier("/Audio/_Onyx/Machines/clock_tick.ogg");

    /// <summary>
    /// Sound played when the trap is armed.
    /// </summary>
    [DataField]
    public SoundSpecifier BeepSound { get; set; } = new SoundPathSpecifier("/Audio/_Onyx/Machines/beep.ogg");

    /// <summary>
    /// Sound played when the trap snaps.
    /// </summary>
    [DataField]
    public SoundSpecifier SnapSound { get; set; } = new SoundPathSpecifier("/Audio/_Onyx/Effects/snap.ogg");

    /// <summary>
    /// Sound played when starting to force the trap on someone.
    /// </summary>
    [DataField]
    public SoundSpecifier StartCuffSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/cuff_start.ogg");

    /// <summary>
    /// Selectable timer options. Escape odds scale with the duration.
    /// </summary>
    [DataField]
    public List<float>? DelayOptions;

    /// <summary>
    /// Base escape chance in percent.
    /// </summary>
    [DataField]
    public float BaseEscapeChance;
}
