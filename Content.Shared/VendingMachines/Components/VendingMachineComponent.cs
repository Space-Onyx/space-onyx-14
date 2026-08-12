using Content.Shared.Actions;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.VendingMachines.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VendingMachineComponent : Component
{
    /// <summary>
    /// PrototypeID for the vending machine's inventory, see <see cref="VendingMachineInventoryPrototype"/>
    /// </summary>
    [DataField("pack", required: true)]
    public ProtoId<VendingMachineInventoryPrototype> PackPrototypeId;

    [DataField]
    public Dictionary<string, VendingMachineInventoryEntry> Inventory = new();

    [DataField]
    public Dictionary<string, VendingMachineInventoryEntry> EmaggedInventory = new();

    [DataField]
    public Dictionary<string, VendingMachineInventoryEntry> ContrabandInventory = new();

    /// <summary>
    /// If true then unlocks the <see cref="ContrabandInventory"/>
    /// </summary>
    [DataField]
    public bool Contraband;

    [DataField]
    public bool Broken;

    /// <summary>
    /// The quality of the stock in the vending machine on spawn.
    /// Represents the percentage chance (0.0f = 0%, 1.0f = 100%) each set of items in the machine is fully-stocked.
    /// If not fully stocked, the stock will have a random value between 0 (inclusive) and max stock (exclusive).
    /// </summary>
    [DataField]
    public float InitialStockQuality = 1.0f;

    /// <summary>
    /// Audio entity used during restock in case the doafter gets canceled.
    /// </summary>
    [DataField]
    public EntityUid? RestockStream;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public double PriceMultiplier = 1;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool AllForFree;

    public ProtoId<StackPrototype> CreditStackPrototype = "Credit";

    [DataField]
    public string CurrencyType = "SpaceCash";

    [DataField]
    public bool UseStaticPrice = true;

    [DataField]
    public bool ShowWithdraw = true;

    [DataField]
    public string BalanceLabel = "vending-ui-credits-amount";

    [DataField]
    public bool InfiniteStock;

    [DataField]
    public SoundSpecifier SoundInsertCurrency =
        new SoundPathSpecifier("/Audio/_Onyx/Machines/polaroid2.ogg");

    [DataField]
    public SoundSpecifier SoundWithdrawCurrency =
        new SoundPathSpecifier("/Audio/_Onyx/Machines/polaroid1.ogg");

    [ViewVariables]
    public int Credits;

    [DataField]
    public Color UiButtonBorderColor = Color.FromHex("#4972A1");

    [DataField]
    public Color UiButtonBaseColor = Color.FromHex("#141F2F");

    [DataField]
    public Color UiButtonHoveredColor = Color.FromHex("#4972A1");

    [DataField]
    public Color UiButtonDisabledColor = Color.FromHex("#3f3f3fff");
}

public sealed partial class VendingMachineSelfDispenseEvent : InstantActionEvent;
