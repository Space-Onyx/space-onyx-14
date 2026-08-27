using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Bitrunning.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NetpodComponent : Component
{
    [DataField]
    public EntityUid? LinkedServer;

    [DataField]
    public EntityUid? Occupant;

    [DataField]
    public EntityUid? Avatar;

    /// <summary>
    /// Internal re-entrancy guard while removing occupant from the pod container.
    /// </summary>
    public bool EjectingOccupant;

    [DataField, AutoNetworkedField]
    public ProtoId<StartingGearPrototype>? PreferredLoadout = "BitrunnerAvatarShaftMinerGear";

    [DataField]
    public List<ProtoId<StartingGearPrototype>> AllowedLoadout = new();

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/door_open.ogg", AudioParams.Default.WithVolume(-2f).WithVariation(0.1f));

    [DataField]
    public SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Effects/door_close.ogg", AudioParams.Default.WithVolume(-2f).WithVariation(0.1f));

    [DataField]
    public SoundSpecifier ConnectStasisSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    [DataField]
    public SoundSpecifier ConnectAvatarSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");

    [DataField]
    public SoundSpecifier DisconnectSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");

    [DataField]
    public SoundSpecifier AutoDisconnectSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splash.ogg");

    [DataField]
    public SoundSpecifier OccupiedPrySound = new SoundPathSpecifier("/Audio/Effects/door_open.ogg");

    [DataField]
    public SoundSpecifier OccupiedPryAlertSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");
}
