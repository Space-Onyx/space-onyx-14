// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using Content.Shared.Access;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Keyring;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KeyringComponent : Component
{
    /// <summary>
    /// How long each attempt takes to open a door.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UnlockAttemptDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The possible access levels.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AccessLevelPrototype>> PossibleAccesses = [];

    /// <summary>
    /// Access levels actually stored on this ring.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<AccessLevelPrototype>> Tags = [];

    /// <summary>
    /// How many access levels will be chosen.
    /// </summary>
    [DataField]
    public int MaxPossibleAccesses = 3;

    /// <summary>
    /// Sound played when starting to pick a door.
    /// </summary>
    [DataField]
    public SoundSpecifier UseSound = new SoundPathSpecifier("/Audio/_Onyx/Items/key_rustle.ogg");
}
