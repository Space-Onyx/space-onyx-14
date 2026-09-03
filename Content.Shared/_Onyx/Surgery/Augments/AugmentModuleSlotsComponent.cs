using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AugmentModuleSlotsComponent : Component
{
    /// <summary>
    /// Module slots exposed by this augment.
    /// </summary>
    [DataField(required: true)]
    public List<AugmentModuleSlotDefinition> Slots = new();

    /// <summary>
    /// Whether installed slot controls are currently exposed.
    /// </summary>
    [AutoNetworkedField]
    public bool PanelOpen;
}

[DataDefinition]
public sealed partial class AugmentModuleSlotDefinition
{
    /// <summary>
    /// Stable item-slot ID.
    /// </summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>
    /// Localized slot name.
    /// </summary>
    [DataField]
    public string Name = "augment-modules-slot-default-name";

    /// <summary>
    /// Whether insertion is allowed before implantation.
    /// </summary>
    [DataField]
    public bool AllowInsertWhenUninstalled = true;

    /// <summary>
    /// Whether insertion is allowed after implantation.
    /// </summary>
    [DataField]
    public bool AllowInsertWhenInstalled = true;

    /// <summary>
    /// Whether this slot appears in augmentation verbs.
    /// </summary>
    [DataField]
    public bool VisibleInVerbs = true;

    /// <summary>
    /// Restriction applied to inserted modules.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;
}

[ByRefEvent]
public readonly record struct AugmentModulePanelStateChangedEvent(EntityUid? Body, bool Open);

[Serializable, NetSerializable]
public sealed partial class AugmentModuleInteractionDoAfterEvent : DoAfterEvent
{
    [DataField]
    public AugmentModuleInteraction Operation;

    [DataField]
    public string SlotId = string.Empty;

    [DataField]
    public bool Open;

    public override DoAfterEvent Clone() => new AugmentModuleInteractionDoAfterEvent
    {
        Operation = Operation,
        SlotId = SlotId,
        Open = Open,
    };
}

[Serializable, NetSerializable]
public enum AugmentModuleInteraction : byte
{
    TogglePanel,
    InsertModule,
    EjectModule,
    InsertCyberDeckItem,
    EjectCyberDeckItem,
}
