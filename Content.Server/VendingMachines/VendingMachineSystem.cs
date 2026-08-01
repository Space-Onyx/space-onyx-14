using System.Linq;
using System.Numerics;
using Content.Server.Cargo.Systems;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Server.Vocalization.Systems;
using Content.Shared._Onyx.Economy;
using Content.Server._Onyx.Economy;
using Content.Shared.Cargo;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.PDA;
using Content.Shared.Power;
using Content.Shared.Stacks;
using Content.Shared.Store.Components;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.UserInterface;
using Content.Shared.VendingMachines;
using Content.Shared.Wall;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.VendingMachines;

public sealed partial class VendingMachineSystem : SharedVendingMachineSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private ThrowingSystem _throwingSystem = default!;
    [Dependency] private BankCardSystem _bankCard = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private StackSystem _stackSystem = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    private static readonly ProtoId<TagPrototype> IgnoreBalanceTag = "IgnoreBalanceChecks";

    private const float WallVendEjectDistanceFromWall = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VendingMachineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<VendingMachineComponent, AfterActivatableUIOpenEvent>(OnAfterUIOpen);
    }

    protected override int GetEntryPrice(EntityPrototype proto, VendingMachineComponent component)
    {
        if (component.UseStaticPrice && proto.TryGetComponent<StaticPriceComponent>(out var staticPrice, EntityManager.ComponentFactory))
            return (int) staticPrice.Price;

        return 5;
    }

    private void OnInteractUsing(EntityUid uid, VendingMachineComponent component, InteractUsingEvent args)
    {
        if (args.Handled || IsSalvageMiningPointVendor(uid) || component.Broken || !_receiver.IsPowered(uid) ||
            !TryComp<CurrencyComponent>(args.Used, out var currency) ||
            !currency.Price.Keys.Contains(component.CurrencyType))
            return;

        component.Credits += Comp<StackComponent>(args.Used).Count;
        Del(args.Used);
        UpdateVendingMachineInterfaceState(uid, component);
        Audio.PlayPvs(component.SoundInsertCurrency, uid);
        args.Handled = true;
    }

    private void OnAfterUIOpen(EntityUid uid, VendingMachineComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateVendingMachineInterfaceState(uid, component);
    }

    protected override void UpdateVendingMachineInterfaceState(EntityUid uid, VendingMachineComponent component)
    {
        _userInterfaceSystem.SetUiState(uid, VendingMachineUiKey.Key,
            new VendingMachineInterfaceState(GetAllInventory(uid, component),
                IsSalvageMiningPointVendor(uid) ? 1 : GetEffectivePriceMultiplier(component),
                IsSalvageMiningPointVendor(uid) ? 0 : component.Credits,
                component.ShowWithdraw, component.BalanceLabel, component.InfiniteStock,
                IsSalvageMiningPointVendor(uid)));
    }

    protected override void OnWithdrawMessage(EntityUid uid, VendingMachineComponent component, VendingMachineWithdrawMessage args)
    {
        if (IsSalvageMiningPointVendor(uid) || component.Credits <= 0)
            return;

        _stackSystem.SpawnAtPosition(component.Credits, component.CreditStackPrototype, Transform(uid).Coordinates);
        component.Credits = 0;
        Audio.PlayPvs(component.SoundWithdrawCurrency, uid);
        UpdateVendingMachineInterfaceState(uid, component);
    }

    protected override void AuthorizedVend(EntityUid uid, EntityUid sender, InventoryType type, string itemId,
        VendingMachineComponent component, int count)
    {
        if (!IsAuthorized(uid, sender, component) || component.Ejecting || component.Broken || !_receiver.IsPowered(uid))
            return;

        var entry = GetEntry(uid, itemId, type, component);
        if (entry == null || count != 1 || (!component.InfiniteStock && entry.Amount <= 0))
        {
            Deny((uid, component), sender);
            return;
        }

        if (TryAuthorizedSalvageMiningPointVend(uid, sender, component, entry))
            return;

        var price = GetPrice(entry, component, count);
        if (price > 0 && !component.AllForFree && !_tag.HasAnyTag(sender, IgnoreBalanceTag))
        {
            var paid = component.Credits >= price;
            if (paid)
                component.Credits -= price;
            else
            {
                foreach (var item in _accessReader.FindPotentialAccessItems(sender))
                {
                    var cardEntity = item;
                    if (TryComp(item, out PdaComponent? pda) && pda.ContainedId is { Valid: true } id)
                        cardEntity = id;

                    if (!TryComp(cardEntity, out BankCardComponent? card) || !card.AccountId.HasValue ||
                        !_bankCard.TryGetAccount(card.AccountId.Value, out var account) || account.Balance < price ||
                        !_bankCard.TryChangeBalance(card.AccountId.Value, -price))
                        continue;

                    paid = true;
                    if (_bankCard.TryGetAccount(card.AccountId.Value, out var buyerAccount))
                    {
                        var itemName = entry.ID;
                        if (ProtoMan.TryIndex<EntityPrototype>(entry.ID, out var proto))
                            itemName = proto.Name;

                        buyerAccount.AddTransaction(new TransactionRecord(
                            TransactionRecord.TransactionType.Purchase,
                            $"Покупка: {itemName}",
                            -price,
                            Color.Red,
                            DateTime.MinValue.Add(_timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan))));
                    }
                    break;
                }
            }

            if (!paid)
            {
                Popup.PopupEntity(Loc.GetString("vending-machine-component-no-balance"), uid, sender);
                Deny((uid, component), sender);
                return;
            }
        }

        component.NextItemCount = count;
        component.EjectEnd = Timing.CurTime + component.EjectDelay;
        component.NextItemToEject = entry.ID;
        component.ThrowNextItem = component.CanShoot;
        if (!component.InfiniteStock)
            entry.Amount--;
        Dirty(uid, component);
        UpdateUI((uid, component));
        TryUpdateVisualState((uid, component));
        UpdateVendingMachineInterfaceState(uid, component);
    }

    [SubscribeLocalEvent]
    private void OnVendingPrice(EntityUid uid, VendingMachineComponent component, ref PriceCalculationEvent args)
    {
        var price = 0.0;

        foreach (var entry in component.Inventory.Values)
        {
            if (!ProtoMan.TryIndex<EntityPrototype>(entry.ID, out var proto))
            {
                Log.Error($"Unable to find entity prototype {entry.ID} on {ToPrettyString(uid)} vending.");
                continue;
            }

            price += entry.Amount * _pricing.GetEstimatedPrice(proto);
        }

        args.Price += price;
    }

    protected override void OnMapInit(EntityUid uid, VendingMachineComponent component, MapInitEvent args)
    {
        base.OnMapInit(uid, component, args);

        if (HasComp<ApcPowerReceiverComponent>(uid))
        {
            TryUpdateVisualState((uid, component));
        }
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(EntityUid uid, VendingMachineComponent component, ref PowerChangedEvent args)
    {
        TryUpdateVisualState((uid, component));
    }

    [SubscribeLocalEvent]
    private void OnDamageChanged(EntityUid uid, VendingMachineComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased && component.Broken)
        {
            component.Broken = false;
            Dirty(uid, component);
            TryUpdateVisualState((uid, component));
            return;
        }

        if (component.Broken || component.DispenseOnHitCoolingDown ||
            component.DispenseOnHitChance == null || args.DamageDelta == null)
            return;

        if (args.DamageIncreased && args.DamageDelta.GetTotal() >= component.DispenseOnHitThreshold &&
            _random.Prob(component.DispenseOnHitChance.Value))
        {
            if (component.DispenseOnHitCooldown != null)
            {
                component.DispenseOnHitEnd = Timing.CurTime + component.DispenseOnHitCooldown.Value;
            }

            EjectRandom(uid, throwItem: true, forceEject: true, component);
        }
    }

    [SubscribeLocalEvent]
    private void OnSelfDispense(EntityUid uid, VendingMachineComponent component, VendingMachineSelfDispenseEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        EjectRandom(uid, throwItem: true, forceEject: false, component);
    }

    /// <summary>
    /// Sets the <see cref="VendingMachineComponent.CanShoot"/> property of the vending machine.
    /// </summary>
    public void SetShooting(EntityUid uid, bool canShoot, VendingMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.CanShoot = canShoot;
    }

    /// <summary>
    /// Sets the <see cref="VendingMachineComponent.Contraband"/> property of the vending machine.
    /// </summary>
    public void SetContraband(EntityUid uid, bool contraband, VendingMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Contraband = contraband;
        Dirty(uid, component);
    }

    /// <summary>
    /// Ejects a random item from the available stock. Will do nothing if the vending machine is empty.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="throwItem">Whether to throw the item in a random direction after dispensing it.</param>
    /// <param name="forceEject">Whether to skip the regular ejection checks and immediately dispense the item without animation.</param>
    /// <param name="vendComponent"></param>
    public void EjectRandom(EntityUid uid, bool throwItem, bool forceEject = false, VendingMachineComponent? vendComponent = null)
    {
        if (!Resolve(uid, ref vendComponent))
            return;

        var availableItems = GetAvailableInventory(uid, vendComponent);
        if (availableItems.Count <= 0)
            return;

        var item = _random.Pick(availableItems);

        if (forceEject)
        {
            vendComponent.NextItemToEject = item.ID;
            vendComponent.ThrowNextItem = throwItem;
            var entry = GetEntry(uid, item.ID, item.Type, vendComponent);
            if (entry != null)
                entry.Amount--;
            EjectItem(uid, vendComponent, forceEject);
        }
        else
        {
            TryEjectVendorItem(uid, item.Type, item.ID, throwItem, user: null, vendComponent: vendComponent);
        }
    }

    protected override void EjectItem(EntityUid uid, VendingMachineComponent? vendComponent = null, bool forceEject = false)
    {
        if (!Resolve(uid, ref vendComponent))
            return;

        // No need to update the visual state because we never changed it during a forced eject
        if (!forceEject)
            TryUpdateVisualState((uid, vendComponent));

        if (string.IsNullOrEmpty(vendComponent.NextItemToEject))
        {
            vendComponent.ThrowNextItem = false;
            return;
        }

        Audio.PlayPvs(vendComponent.SoundVend, uid); // <Onyx-VendingSound>

        // Default spawn coordinates
        var xform = Transform(uid);
        var spawnCoordinates = xform.Coordinates;

        //Make sure the wallvends spawn outside of the wall.
        if (TryComp<WallMountComponent>(uid, out var wallMountComponent))
        {
            var offset = (wallMountComponent.Direction + xform.LocalRotation - Math.PI / 2).ToVec() * WallVendEjectDistanceFromWall;
            spawnCoordinates = spawnCoordinates.Offset(offset);
        }

        for (var i = 0; i < vendComponent.NextItemCount; i++)
        {
            var ent = Spawn(vendComponent.NextItemToEject, spawnCoordinates);

            if (vendComponent.ThrowNextItem)
            {
                var range = vendComponent.NonLimitedEjectRange;
                var direction = new Vector2(_random.NextFloat(-range, range), _random.NextFloat(-range, range));
                _throwingSystem.TryThrow(ent, direction, vendComponent.NonLimitedEjectForce);
            }
        }

        vendComponent.NextItemToEject = null;
        vendComponent.ThrowNextItem = false;
        vendComponent.NextItemCount = 1;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var disabled = EntityQueryEnumerator<EmpDisabledComponent, VendingMachineComponent>();
        while (disabled.MoveNext(out var uid, out _, out var comp))
        {
            if (comp.NextEmpEject < Timing.CurTime)
            {
                EjectRandom(uid, true, false, comp);
                comp.NextEmpEject += (5 * comp.EjectDelay);
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnPriceCalculation(EntityUid uid, VendingMachineRestockComponent component, ref PriceCalculationEvent args)
    {
        List<double> priceSets = new();

        // Find the most expensive inventory and use that as the highest price.
        foreach (var vendingInventory in component.CanRestock)
        {
            double total = 0;

            if (ProtoMan.TryIndex(vendingInventory, out VendingMachineInventoryPrototype? inventoryPrototype))
            {
                foreach (var (item, amount) in inventoryPrototype.StartingInventory)
                {
                    if (ProtoMan.TryIndex(item, out EntityPrototype? entity))
                        total += _pricing.GetEstimatedPrice(entity) * amount;
                }
            }

            priceSets.Add(total);
        }

        args.Price += priceSets.Max();
    }

    [SubscribeLocalEvent]
    private void OnTryVocalize(Entity<VendingMachineComponent> ent, ref TryVocalizeEvent args)
    {
        args.Cancelled |= ent.Comp.Broken;
    }
}
