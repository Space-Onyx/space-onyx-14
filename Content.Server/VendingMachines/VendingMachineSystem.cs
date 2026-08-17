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
        if (!Resolve(entity.Owner, ref entity.Comp1, ref entity.Comp2) || entity.Comp2.NextItemToEject is not { } item) { if (entity.Comp2 != null) entity.Comp2.ThrowNextItem = false; return; }
        var coordinates = Transform(entity.Owner).Coordinates;
        if (TryComp<WallMountComponent>(entity.Owner, out var wall)) coordinates = coordinates.Offset((wall.Direction + Transform(entity.Owner).LocalRotation - Math.PI / 2).ToVec());
        var spawned = Spawn(item, coordinates);
        if (entity.Comp2.ThrowNextItem) _throwingSystem.TryThrow(spawned, new Vector2(_random.NextFloat(-entity.Comp2.NonLimitedEjectRange, entity.Comp2.NonLimitedEjectRange), _random.NextFloat(-entity.Comp2.NonLimitedEjectRange, entity.Comp2.NonLimitedEjectRange)), entity.Comp2.NonLimitedEjectForce);
        entity.Comp2.NextItemToEject = null; entity.Comp2.ThrowNextItem = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<EmpDisabledComponent, VendingMachineComponent, VendingMachineEjectComponent>();
        while (query.MoveNext(out var uid, out _, out var comp, out var eject)) if (eject.NextEmpEject < Timing.CurTime) { EjectRandom((uid, comp, eject), true); eject.NextEmpEject += 5 * eject.EjectDelay; }
    }

    [SubscribeLocalEvent]
    private void OnVendingPrice(Entity<VendingMachineComponent> entity, ref PriceCalculationEvent args)
    {
        foreach (var entry in entity.Comp.Inventory.Values) if (ProtoMan.TryIndex<EntityPrototype>(entry.ID, out var proto)) args.Price += entry.Amount * _pricing.GetEstimatedPrice(proto);
    }

    [SubscribeLocalEvent]
    private void OnTryVocalize(Entity<VendingMachineComponent> ent, ref TryVocalizeEvent args) => args.Cancelled |= ent.Comp.Broken;

    public void SetShooting(Entity<VendingMachineEjectComponent?> entity, bool canShoot) { if (canShoot) EnsureComp<VendingMachineShootComponent>(entity.Owner); else RemComp<VendingMachineShootComponent>(entity.Owner); }

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
