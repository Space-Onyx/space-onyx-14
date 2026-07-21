using Content.Shared.Radio.EntitySystems;
using Content.Shared.Chat;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Radio.Components;

/// <summary>
///     Listens for radio messages and relays them to local chat.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRadioDeviceSystem))]
public sealed partial class RadioSpeakerComponent : Component
{
    /// <summary>
    /// Whether or not interacting with this entity
    /// toggles it on or off.
    /// </summary>
    [DataField]
    public bool ToggleOnInteract = true;

    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = new() { SharedChatSystem.CommonChannel };

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
