using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AugmentItemPanelComponent : Component
{
    public const string DefaultContainerId = "item_panel_storage";

    [DataField(required: true)]
    public EntProtoId ItemPrototype;

    [DataField]
    public SpriteSpecifier? Icon;

    [DataField, AutoNetworkedField]
    public EntityUid? SpawnedItem;

    [DataField]
    public string StorageContainerId = DefaultContainerId;

    [DataField, AutoNetworkedField]
    public bool IsEquipped;

    [DataField]
    public float ExtendPowerCost = 2f;

    [DataField]
    public float RetractPowerCost = 2f;

    [DataField]
    public bool RequiresPower = true;

    [DataField]
    public TimeSpan ActionCooldown = TimeSpan.FromSeconds(2);

    [DataField]
    public SoundSpecifier? ExtendSound;

    [DataField]
    public SoundSpecifier? RetractSound;

    [DataField]
    public string? ExtendHeldPrefix;

    [DataField]
    public TimeSpan ExtendHeldPrefixDuration = TimeSpan.FromSeconds(0.3);

    [DataField]
    public string? ExtendHeldPrefixAfter;
}

public sealed partial class AugmentItemPanelActionEvent : InstantActionEvent;
