using Content.Shared.Radio.EntitySystems;
using Content.Shared.Chat;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Radio.Components;

/// <summary>
/// Listens for radio messages and relays them to local chat.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRadioDeviceSystem))]
public sealed partial class RadioSpeakerComponent : Component
{
    /// <summary>
    /// Whether interacting with this entity toggles it on/off, or not.
    /// </summary>
    [DataField]
    public bool ToggleOnInteract = true;

    /// <summary>
    /// Radio channels from which messages are received.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = new() { SharedChatSystem.CommonChannel };

    /// <summary>
    /// Whether the speaker is currently receiving radio messages.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled;

    // <Onyx-StationRadio>
    /// <summary>
    /// Relays received messages as normal speech instead of whispers.
    /// </summary>
    [DataField]
    public bool SpeakNormally;

    /// <summary>
    /// Whether the speaker requires machine power to receive messages.
    /// </summary>
    [DataField]
    public bool PowerRequired;
    // </Onyx-StationRadio>
}
