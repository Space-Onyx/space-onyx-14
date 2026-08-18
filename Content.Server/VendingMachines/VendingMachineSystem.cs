using System.Linq;
using System.Numerics;
using Content.Server.Cargo.Systems;
using Content.Server._Onyx.Economy;
using Content.Server.GameTicking;
using Content.Server.Stack;
using Content.Server.VendingMachines.Components;
using Content.Server.Vocalization.Systems;
using Content.Shared._Onyx.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo;
using Content.Shared.CCVar;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.PDA;
using Content.Shared.Power;
using Content.Shared.Stacks;
using Content.Shared.Store.Components;
using Content.Server.Cargo.Components;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.UserInterface;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Content.Shared.Wall;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
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
    [Dependency] private AccessReaderSystem _serverAccess = default!;
    private static readonly ProtoId<TagPrototype> IgnoreBalanceTag = "IgnoreBalanceChecks";
    private const float WallVendEjectDistanceFromWall = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VendingMachineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<VendingMachineComponent, AfterActivatableUIOpenEvent>(OnAfterUIOpen);
    }

    protected override int GetEntryPrice(EntityPrototype proto, VendingMachineComponent component) =>
        component.UseStaticPrice && proto.TryGetComponent<StaticPriceComponent>(out var price, EntityManager.ComponentFactory) ? (int) price.Price : 5;

    private void OnInteractUsing(EntityUid uid, VendingMachineComponent component, InteractUsingEvent args)
    {
        if (args.Handled || IsSalvageMiningPointVendor(uid) || component.Broken || !_receiver.IsPowered(uid) || !TryComp<CurrencyComponent>(args.Used, out var currency) || !currency.Price.Keys.Contains(component.CurrencyType)) return;
        component.Credits += Comp<StackComponent>(args.Used).Count; Del(args.Used); UpdateVendingMachineInterfaceState(uid, component); Audio.PlayPvs(component.SoundInsertCurrency, uid); args.Handled = true;
    }

    private void OnAfterUIOpen(EntityUid uid, VendingMachineComponent component, AfterActivatableUIOpenEvent args) => UpdateVendingMachineInterfaceState(uid, component);

    protected override void UpdateVendingMachineInterfaceState(EntityUid uid, VendingMachineComponent component)
    {
        _userInterfaceSystem.SetUiState(uid, VendingMachineUiKey.Key, new VendingMachineInterfaceState(GetAllInventory(uid, component), IsSalvageMiningPointVendor(uid) ? 1 : component.PriceMultiplier * _cfg.GetCVar(CCVars.VendingPriceMultiplier), IsSalvageMiningPointVendor(uid) ? 0 : component.Credits, component.ShowWithdraw, component.BalanceLabel, component.InfiniteStock, IsSalvageMiningPointVendor(uid)));
    }

    protected override void OnWithdrawMessage(EntityUid uid, VendingMachineComponent component, VendingMachineWithdrawMessage args)
    {
        if (IsSalvageMiningPointVendor(uid) || component.Credits <= 0) return;
        _stackSystem.SpawnAtPosition(component.Credits, component.CreditStackPrototype, Transform(uid).Coordinates); component.Credits = 0; Audio.PlayPvs(component.SoundWithdrawCurrency, uid); UpdateVendingMachineInterfaceState(uid, component);
    }

    protected override void AuthorizedVend(EntityUid uid, EntityUid sender, InventoryType type, string itemId, VendingMachineComponent component, int count)
    {
        if (!IsAuthorized(uid, sender, component) || !TryComp<VendingMachineEjectComponent>(uid, out var eject) || eject.Ejecting || component.Broken || !_receiver.IsPowered(uid)) return;
        var entry = GetEntry(uid, itemId, type, component);
        if (entry == null || count != 1 || (!component.InfiniteStock && entry.Amount <= 0)) { Deny((uid, component), sender, eject); return; }
        if (TryAuthorizedSalvageMiningPointVend(uid, sender, component, entry)) return;
        var price = (int) (entry.Price * count * component.PriceMultiplier * _cfg.GetCVar(CCVars.VendingPriceMultiplier));
        if (price > 0 && !component.AllForFree && !_tag.HasAnyTag(sender, IgnoreBalanceTag))
        {
            var paid = component.Credits >= price;
            if (paid) component.Credits -= price;
            else foreach (var item in _serverAccess.FindPotentialAccessItems(sender))
            {
                var cardEntity = item;
                if (TryComp(item, out PdaComponent? pda) && pda.ContainedId is { Valid: true } id) cardEntity = id;
                if (!TryComp(cardEntity, out BankCardComponent? card) || !card.AccountId.HasValue || !_bankCard.TryGetAccount(card.AccountId.Value, out var account) || account.Balance < price || !_bankCard.TryChangeBalance(card.AccountId.Value, -price)) continue;
                // <Onyx-VendingPurchaseHistory>
                var itemName = ProtoMan.TryIndex<EntityPrototype>(entry.ID, out var proto) ? proto.Name : entry.ID;
                account.AddTransaction(new TransactionRecord(
                    TransactionRecord.TransactionType.Purchase,
                    $"Покупка: {itemName}",
                    -price,
                    Color.Red,
                    DateTime.MinValue.Add(_timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan))));
                // </Onyx-VendingPurchaseHistory>
                paid = true;
                break;
            }
            if (!paid) { Popup.PopupEntity(Loc.GetString("vending-machine-component-no-balance"), uid, sender); Deny((uid, component), ejectComponent: eject); return; } // <Onyx-VendingPaymentSound-edited>
        }
        TryEjectVendorItem(uid, type, itemId, ShouldThrowVendItem((uid, eject)), sender, component, eject);
        UpdateVendingMachineInterfaceState(uid, component);
    }

    protected override bool ShouldThrowVendItem(Entity<VendingMachineEjectComponent> entity) => HasComp<VendingMachineShootComponent>(entity.Owner);

    protected override void EjectItem(Entity<VendingMachineComponent?, VendingMachineEjectComponent?> entity, bool forceEject = false)
    {
        if (!Resolve(entity.Owner, ref entity.Comp1, ref entity.Comp2))
            return;

        var uid = entity.Owner;
        var ejectComponent = entity.Comp2;

        if (ejectComponent.NextItemToEject is not { } item)
        {
            ejectComponent.ThrowNextItem = false;
            return;
        }

        var xform = Transform(uid);
        var spawnCoordinates = xform.Coordinates;
        if (TryComp<WallMountComponent>(uid, out var wallMountComponent))
        {
            var offset = (wallMountComponent.Direction + xform.LocalRotation - Math.PI / 2).ToVec() * WallVendEjectDistanceFromWall;
            spawnCoordinates = spawnCoordinates.Offset(offset);
        }

        var spawned = Spawn(item, spawnCoordinates);
        if (ejectComponent.ThrowNextItem)
        {
            var range = ejectComponent.NonLimitedEjectRange;
            var direction = new Vector2(_random.NextFloat(-range, range), _random.NextFloat(-range, range));
            _throwingSystem.TryThrow(spawned, direction, ejectComponent.NonLimitedEjectForce);
        }

        ejectComponent.NextItemToEject = null;
        ejectComponent.ThrowNextItem = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = Timing.CurTime;
        var dispenseOnHitQuery = EntityQueryEnumerator<VendingMachineDispenseOnHitComponent>();
        while (dispenseOnHitQuery.MoveNext(out _, out var dispenseOnHit))
        {
            if (dispenseOnHit.NextDispenseTime is not { } nextDispenseTime || curTime <= nextDispenseTime)
                continue;

            dispenseOnHit.NextDispenseTime = null;
        }

        var disabled = EntityQueryEnumerator<EmpDisabledComponent, VendingMachineComponent, VendingMachineEjectComponent>();
        while (disabled.MoveNext(out var uid, out _, out var comp, out var eject))
        {
            if (eject.NextEmpEject >= curTime)
                continue;

            EjectRandom((uid, comp, eject), true, false);
            eject.NextEmpEject += 5 * eject.EjectDelay;
        }
    }

    [SubscribeLocalEvent]
    private void OnVendingPrice(Entity<VendingMachineComponent> entity, ref PriceCalculationEvent args)
    {
        var price = 0.0;

        foreach (var entry in entity.Comp.Inventory.Values)
        {
            if (!ProtoMan.TryIndex<EntityPrototype>(entry.ID, out var proto))
            {
                Log.Error($"Unable to find entity prototype {entry.ID} on {ToPrettyString(entity)} vending.");
                continue;
            }

            price += entry.Amount * _pricing.GetEstimatedPrice(proto);
        }

        args.Price += price;
    }

    [SubscribeLocalEvent]
    private void OnDamageChanged(Entity<VendingMachineComponent> entity, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased && entity.Comp.Broken)
        {
            entity.Comp.Broken = false;
            Dirty(entity);
            return;
        }

        if (!TryComp<VendingMachineDispenseOnHitComponent>(entity.Owner, out var dispenseOnHit) ||
            entity.Comp.Broken ||
            dispenseOnHit.CoolingDown ||
            args.DamageDelta == null)
        {
            return;
        }

        if (!args.DamageIncreased ||
            args.DamageDelta.GetTotal() < dispenseOnHit.Threshold ||
            !_random.Prob(dispenseOnHit.Chance))
        {
            return;
        }

        if (dispenseOnHit.NextDispenseDelay != null)
            dispenseOnHit.NextDispenseTime = Timing.CurTime + dispenseOnHit.NextDispenseDelay.Value;

        EjectRandom((entity.Owner, entity.Comp), throwItem: true, forceEject: true);
    }

    [SubscribeLocalEvent]
    private void OnSelfDispense(Entity<VendingMachineComponent> entity, ref VendingMachineSelfDispenseEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        EjectRandom((entity.Owner, entity.Comp), throwItem: true, forceEject: false);
    }

    [SubscribeLocalEvent]
    private void OnPriceCalculation(Entity<VendingMachineRestockComponent> entity, ref PriceCalculationEvent args)
    {
        List<double> priceSets = new();

        foreach (var vendingInventory in entity.Comp.CanRestock)
        {
            double total = 0;

            if (ProtoMan.TryIndex(vendingInventory, out VendingMachineInventoryPrototype? inventoryPrototype))
            {
                foreach (var (item, amount) in inventoryPrototype.StartingInventory)
                {
                    if (ProtoMan.TryIndex(item, out EntityPrototype? prototype))
                        total += _pricing.GetEstimatedPrice(prototype) * amount;
                }
            }

            priceSets.Add(total);
        }

        args.Price += priceSets.Max();
    }

    [SubscribeLocalEvent]
    private void OnTryVocalize(Entity<VendingMachineComponent> ent, ref TryVocalizeEvent args) => args.Cancelled |= ent.Comp.Broken;

    public void SetShooting(Entity<VendingMachineEjectComponent?> entity, bool canShoot)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return;

        if (canShoot)
            EnsureComp<VendingMachineShootComponent>(entity.Owner);
        else
            RemComp<VendingMachineShootComponent>(entity.Owner);
    }

    public void SetContraband(Entity<VendingMachineComponent> entity, bool contraband) { entity.Comp.Contraband = contraband; Dirty(entity); }

    public void EjectRandom(Entity<VendingMachineComponent?, VendingMachineEjectComponent?> entity, bool throwItem, bool forceEject = false)
    {
        if (!Resolve(entity.Owner, ref entity.Comp1, ref entity.Comp2)) return;
        var available = GetAvailableInventory(entity.Owner, entity.Comp1);
        if (available.Count == 0) return;
        var item = _random.Pick(available);
        if (forceEject) { entity.Comp2.NextItemToEject = item.ID; entity.Comp2.ThrowNextItem = throwItem; if (!entity.Comp1.InfiniteStock) GetEntry(entity.Owner, item.ID, item.Type, entity.Comp1)!.Amount--; Dirty(entity.Owner, entity.Comp1); Audio.PlayPvs(entity.Comp2.SoundVend, entity.Owner); EjectItem(entity, true); } // <Onyx-VendingForcedEjectSound-edited>
        else TryEjectVendorItem(entity.Owner, item.Type, item.ID, throwItem, vendComponent: entity.Comp1, ejectComponent: entity.Comp2);
    }
}
