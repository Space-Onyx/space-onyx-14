using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.ItemSwitch;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ItemSwitchComponent : Component
{
    [DataField, AutoNetworkedField]
    public string State = string.Empty;

    [DataField(readOnly: true)]
    public Dictionary<string, ItemSwitchState> States = new();

    [DataField]
    public bool OnActivate = true;

    [DataField]
    public bool OnUse = true;

    [DataField]
    public bool Predictable = true;

    [DataField]
    public bool ShowLabel;

    [DataField]
    public bool NeedsPower;

    [DataField]
    public bool IsPowered = true;

    [DataField]
    public string? DefaultState;
}

[DataDefinition]
public sealed partial class ItemSwitchState
{
    [DataField]
    public string? Verb;

    [DataField]
    public ComponentRegistry? Components;

    [DataField]
    public bool RemoveComponents = true;

    [DataField]
    public SpriteSpecifier? Sprite;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public SoundSpecifier? SoundStateActivate;

    [DataField]
    public SoundSpecifier? SoundFailToActivate;

    [DataField]
    public bool Hidden;

    [DataField]
    public int EnergyPerUse;
}

[ByRefEvent]
public record struct ItemSwitchAttemptEvent(EntityUid? User, string State)
{
    public bool Cancelled;
}

[ByRefEvent]
public readonly record struct ItemSwitchedEvent(EntityUid? User, string State, bool Predicted);
