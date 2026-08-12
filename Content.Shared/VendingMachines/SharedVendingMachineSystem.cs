using System.Linq;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Advertise.Components;
using Content.Shared.Advertise.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.VendingMachines.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared._Onyx.Economy;
using Content.Shared.CCVar;

namespace Content.Shared.VendingMachines;

public abstract partial class SharedVendingMachineSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected SharedPowerReceiverSystem _receiver = default!;
    [Dependency] protected SharedUserInterfaceSystem UISystem = default!;
    [Dependency] protected IRobustRandom Randomizer = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<VendingMachineComponent>(VendingMachineUiKey.Key, subs =>
        {
            subs.Event<VendingMachineEjectMessage>(OnInventoryEjectMessage);
            subs.Event<VendingMachineEjectCountMessage>(OnInventoryEjectCountMessage);
            subs.Event<VendingMachineWithdrawMessage>(OnWithdrawMessage);
        });
    }

    [SubscribeLocalEvent]
    private void OnVendingGetState(Entity<VendingMachineComponent> entity, ref ComponentGetState args)
    {
        var state = new VendingMachineComponentState
        {
            Contraband = entity.Comp.Contraband,
            Broken = entity.Comp.Broken,
            AllForFree = entity.Comp.AllForFree,
            UiButtonBorderColor = entity.Comp.UiButtonBorderColor,
            UiButtonBaseColor = entity.Comp.UiButtonBaseColor,
            UiButtonHoveredColor = entity.Comp.UiButtonHoveredColor,
            UiButtonDisabledColor = entity.Comp.UiButtonDisabledColor,
        };
        CopyInventory(entity.Comp.Inventory, state.Inventory);
        CopyInventory(entity.Comp.EmaggedInventory, state.EmaggedInventory);
        CopyInventory(entity.Comp.ContrabandInventory, state.ContrabandInventory);
        args.State = state;
    }

    protected static void CopyInventory(Dictionary<string, VendingMachineInventoryEntry> source, Dictionary<string, VendingMachineInventoryEntry> target)
    {
        target.Clear();
        foreach (var entry in source) target.Add(entry.Key, new(entry.Value));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<VendingMachineComponent, VendingMachineEjectComponent>();
        var curTime = Timing.CurTime;
        while (query.MoveNext(out var uid, out var comp, out var eject)) UpdateEjectState((uid, comp, eject), curTime);
    }

    protected virtual int GetEntryPrice(EntityPrototype proto, VendingMachineComponent component) => 5;

    private void OnInventoryEjectCountMessage(EntityUid uid, VendingMachineComponent component, VendingMachineEjectCountMessage args)
    {
        if (!_receiver.IsPowered(uid) || args.Actor is not { Valid: true } actor || Deleted(actor)) return;
        AuthorizedVend(uid, actor, args.Entry.Type, args.Entry.ID, component, args.Count);
    }

    protected virtual void AuthorizedVend(EntityUid uid, EntityUid sender, InventoryType type, string itemId, VendingMachineComponent component, int count)
    {
        if (!IsAuthorized(uid, sender, component) || !TryComp<VendingMachineEjectComponent>(uid, out var eject)) return;
        TryEjectVendorItem(uid, type, itemId, ShouldThrowVendItem((uid, eject)), sender, component, eject);
    }

    protected virtual int GetPrice(VendingMachineInventoryEntry entry, VendingMachineComponent comp, int count) =>
        (int) (entry.Price * count * comp.PriceMultiplier * _cfg.GetCVar(CCVars.VendingPriceMultiplier));

    protected virtual void UpdateVendingMachineInterfaceState(EntityUid uid, VendingMachineComponent component) { }
    protected virtual void OnWithdrawMessage(EntityUid uid, VendingMachineComponent component, VendingMachineWithdrawMessage args) => UpdateVendingMachineInterfaceState(uid, component);

    [SubscribeLocalEvent]
    protected virtual void OnMapInit(EntityUid uid, VendingMachineComponent component, MapInitEvent args) => RestockInventoryFromPrototype(uid, component, component.InitialStockQuality);

    [SubscribeLocalEvent]
    private void OnEmagged(EntityUid uid, VendingMachineComponent component, ref GotEmaggedEvent args)
    {
        if (_emag.CompareFlag(args.Type, EmagType.Interaction) && !_emag.CheckFlag(uid, EmagType.Interaction)) args.Handled = component.EmaggedInventory.Count > 0 || component.PriceMultiplier > 0;
    }

    [SubscribeLocalEvent]
    private void OnActivatableUIOpenAttempt(EntityUid uid, VendingMachineComponent component, ActivatableUIOpenAttemptEvent args) { if (component.Broken) args.Cancel(); }

    [SubscribeLocalEvent]
    private void OnBreak(EntityUid uid, VendingMachineComponent component, BreakageEventArgs args) { component.Broken = true; Dirty(uid, component); UISystem.CloseUi(uid, VendingMachineUiKey.Key); }

    protected virtual void UpdateUI(Entity<VendingMachineComponent?> entity) { }

    public List<VendingMachineInventoryEntry> GetAllInventory(EntityUid uid, VendingMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        var inventory = new List<VendingMachineInventoryEntry>(component.Inventory.Values);
        if (_emag.CheckFlag(uid, EmagType.Interaction))
            inventory.AddRange(component.EmaggedInventory.Values);
        if (component.Contraband)
            inventory.AddRange(component.ContrabandInventory.Values);

        return inventory;
    }

    public List<VendingMachineInventoryEntry> GetAvailableInventory(EntityUid uid, VendingMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        return GetAllInventory(uid, component).Where(entry => component.InfiniteStock || entry.Amount > 0).ToList();
    }

    public void RestockInventoryFromPrototype(EntityUid uid,
        VendingMachineComponent? component = null,
        float restockQuality = 1f)
    {
        if (!Resolve(uid, ref component) ||
            !ProtoMan.TryIndex(component.PackPrototypeId, out VendingMachineInventoryPrototype? pack))
            return;

        AddInventoryFromPrototype(uid, pack.StartingInventory, InventoryType.Regular, pack, component, restockQuality);
        AddInventoryFromPrototype(uid, pack.EmaggedInventory, InventoryType.Emagged, pack, component, restockQuality);
        AddInventoryFromPrototype(uid, pack.ContrabandInventory, InventoryType.Contraband, pack, component, restockQuality);
        Dirty(uid, component);
    }

    private void AddInventoryFromPrototype(EntityUid uid,
        Dictionary<string, uint>? entries,
        InventoryType type,
        VendingMachineInventoryPrototype pack,
        VendingMachineComponent? component,
        float restockQuality)
    {
        if (!Resolve(uid, ref component) || entries == null)
            return;

        var inventory = type switch
        {
            InventoryType.Regular => component.Inventory,
            InventoryType.Emagged => component.EmaggedInventory,
            InventoryType.Contraband => component.ContrabandInventory,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        var order = 0;
        foreach (var (id, amount) in entries)
        {
            if (!ProtoMan.TryIndex<EntityPrototype>(id, out var proto))
            {
                order++;
                continue;
            }

            var restock = amount;
            var missingStockChance = 1 - restockQuality;
            var result = Randomizer.NextFloat();
            if (result < missingStockChance)
                restock = (uint) Math.Floor(amount * result / missingStockChance);

            if (inventory.TryGetValue(id, out var entry))
            {
                entry.Amount = Math.Min(entry.Amount + restock, 3 * amount);
            }
            else
            {
                var price = pack.Prices.TryGetValue(id, out var configuredPrice)
                    ? configuredPrice
                    : GetEntryPrice(proto, component);
                inventory.Add(id, new VendingMachineInventoryEntry(type,
                    id,
                    restock,
                    price,
                    pack.Categories.GetValueOrDefault(id),
                    pack.OverrideNames.GetValueOrDefault(id),
                    order));
            }

            order++;
        }
    }
}
