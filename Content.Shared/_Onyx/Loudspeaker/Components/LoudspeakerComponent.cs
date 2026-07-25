using Content.Shared.Inventory;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Loudspeaker.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LoudspeakerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool WorksInHand;

    [DataField, AutoNetworkedField]
    public bool CanToggle;

    [DataField, AutoNetworkedField]
    public bool IsActive;

    [DataField, AutoNetworkedField]
    public bool AffectChat;

    [DataField, AutoNetworkedField]
    public bool AffectRadio;

    [DataField, AutoNetworkedField]
    public int FontSize = 18;

    [DataField]
    public SlotFlags RequiredSlot = SlotFlags.EARS;

    [DataField]
    public SoundPathSpecifier ToggleSound = new("/Audio/Items/pen_click.ogg");

    [DataField]
    public ProtoId<SpeechSoundsPrototype>? SpeechSounds;
}
