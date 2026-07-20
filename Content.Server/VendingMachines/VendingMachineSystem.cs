using System.Linq;
using System.Numerics;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Server.Vocalization.Systems;
using Content.Server._Onyx.Economy;
using Content.Shared._Onyx.Economy;
using Content.Shared.Advertise.Components;
using Content.Shared.Emp;
using Content.Shared.Cargo;
using Content.Shared.Damage.Systems;
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
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.VendingMachines
{
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

        private const float WallVendEjectDistanceFromWall = 1f;
        private static readonly ProtoId<TagPrototype> IgnoreBalanceTag = "IgnoreBalanceChecks";

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<VendingMachineComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<VendingMachineComponent, DamageChangedEvent>(OnDamageChanged);
            SubscribeLocalEvent<VendingMachineComponent, PriceCalculationEvent>(OnVendingPrice);
            SubscribeLocalEvent<VendingMachineComponent, TryVocalizeEvent>(OnTryVocalize);

            SubscribeLocalEvent<VendingMachineComponent, VendingMachineSelfDispenseEvent>(OnSelfDispense);

            SubscribeLocalEvent<VendingMachineRestockComponent, PriceCalculationEvent>(OnPriceCalculation);

            SubscribeLocalEvent<VendingMachineComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<VendingMachineComponent, AfterActivatableUIOpenEvent>(OnAfterUIOpen);
        }

        protected override int GetEntryPrice(EntityPrototype proto, VendingMachineComponent component)
        {
            if (component.UseStaticPrice && proto.TryGetComponent<StaticPriceComponent>(out var staticPrice, EntityManager.ComponentFactory))
            {
                return (int)staticPrice.Price;
            }
            return 5;
        }

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

        protected override void UpdateVendingMachineInterfaceState(EntityUid uid, VendingMachineComponent component)
        {
            var state = new VendingMachineInterfaceState(GetAllInventory(uid, component), component.PriceMultiplier,
                component.Credits);

            _userInterfaceSystem.SetUiState(uid, VendingMachineUiKey.Key, state);
        }

        private void OnPowerChanged(EntityUid uid, VendingMachineComponent component, ref PowerChangedEvent args)
        {
            TryUpdateVisualState((uid, component));
        }

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

        private void OnSelfDispense(EntityUid uid, VendingMachineComponent component, VendingMachineSelfDispenseEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            EjectRandom(uid, throwItem: true, forceEject: false, component);
        }

        private void OnInteractUsing(EntityUid uid, VendingMachineComponent component, InteractUsingEvent args)
        {
            if (args.Handled)
                return;

            if (component.Broken || !_receiver.IsPowered(uid))
                return;

            if (!TryComp<CurrencyComponent>(args.Used, out var currency) ||
                !currency.Price.Keys.Contains(component.CurrencyType))
                return;

            var stack = Comp<StackComponent>(args.Used);
            component.Credits += stack.Count;
            Del(args.Used);
            UpdateVendingMachineInterfaceState(uid, component);
            Audio.PlayPvs(component.SoundInsertCurrency, uid);
            args.Handled = true;
        }

        protected override void OnWithdrawMessage(EntityUid uid, VendingMachineComponent component, VendingMachineWithdrawMessage args)
        {
            if (component.Credits <= 0)
                return;

            _stackSystem.SpawnAtPosition(component.Credits, component.CreditStackPrototype,
                Transform(uid).Coordinates);
            component.Credits = 0;
            Audio.PlayPvs(component.SoundWithdrawCurrency, uid);

            UpdateVendingMachineInterfaceState(uid, component);
        }

        protected override void AuthorizedVend(EntityUid uid, EntityUid sender, InventoryType type, string itemId, VendingMachineComponent component, int count)
        {
            if (!IsAuthorized(uid, sender, component))
                return;

            if (component.Ejecting || component.Broken || !_receiver.IsPowered(uid))
                return;

            var entry = GetEntry(uid, itemId, type, component);

            if (entry == null)
            {
                if (sender.IsValid())
                    Popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-invalid-item"), uid, sender);

                Deny((uid, component));
                return;
            }

            if (entry.Amount <= 0)
            {
                if (sender.IsValid())
                    Popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-out-of-stock"), uid, sender);

                Deny((uid, component));
                return;
            }

            if (string.IsNullOrEmpty(entry.ID))
                return;

            var price = GetPrice(entry, component, count);
            if (price > 0 && !component.AllForFree && sender.IsValid() && !_tag.HasAnyTag(sender, IgnoreBalanceTag))
            {
                var success = false;
                if (component.Credits >= price)
                {
                    component.Credits -= price;
                    success = true;
                }
                else
                {
                    var items = _accessReader.FindPotentialAccessItems(sender);
                    foreach (var item in items)
                    {
                        var nextItem = item;
                        if (TryComp(item, out PdaComponent? pda) && pda.ContainedId is { Valid: true } id)
                            nextItem = id;

                        if (!TryComp<BankCardComponent>(nextItem, out var bankCard) || !bankCard.AccountId.HasValue
                            || !_bankCard.TryGetAccount(bankCard.AccountId.Value, out var account)
                            || account.Balance < price)
                            continue;

                        if (_bankCard.TryChangeBalance(bankCard.AccountId.Value, -price))
                        {
                            success = true;
                            if (_bankCard.TryGetAccount(bankCard.AccountId.Value, out var buyerAccount))
                            {
                                var itemName = entry.ID;
                                if (ProtoMan.TryIndex<EntityPrototype>(entry.ID, out var proto))
                                    itemName = proto.Name;
                                var now = _timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                                buyerAccount.AddTransaction(new TransactionRecord(
                                    TransactionRecord.TransactionType.Purchase,
                                    $"Покупка: {itemName}",
                                    -price,
                                    Robust.Shared.Maths.Color.Red,
                                    DateTime.MinValue.Add(now)
                                ));
                            }
                        }
                        break;
                    }
                }

                if (!success)
                {
                    Popup.PopupEntity(Loc.GetString("vending-machine-component-no-balance"), uid);
                    Deny((uid, component));
                    return;
                }
            }

            component.NextItemCount = count;

            component.EjectEnd = Timing.CurTime + component.EjectDelay;
            component.NextItemToEject = entry.ID;
            component.ThrowNextItem = component.CanShoot;

            if (TryComp(uid, out SpeakOnUIClosedComponent? speakComponent))
                _speakOn.TrySetFlag((uid, speakComponent));

            entry.Amount -= (uint)count;
            Dirty(uid, component);
            UpdateUI((uid, component));
            TryUpdateVisualState((uid, component));
            UpdateVendingMachineInterfaceState(uid, component);
        }

        protected override void EjectItem(EntityUid uid, VendingMachineComponent? vendComponent = null, bool forceEject = false)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            if (!forceEject)
                TryUpdateVisualState((uid, vendComponent));

            if (string.IsNullOrEmpty(vendComponent.NextItemToEject))
            {
                vendComponent.ThrowNextItem = false;
                return;
            }

            var xform = Transform(uid);
            var spawnCoordinates = xform.Coordinates;

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

            if (vendComponent.NextItemCount > 0)
                Audio.PlayPvs(vendComponent.SoundVend, uid);

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

        public void SetShooting(EntityUid uid, bool canShoot, VendingMachineComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.CanShoot = canShoot;
        }

        public void SetContraband(EntityUid uid, bool contraband, VendingMachineComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.Contraband = contraband;
            Dirty(uid, component);
        }

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
                vendComponent.NextItemCount = 1;
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

        private void OnPriceCalculation(EntityUid uid, VendingMachineRestockComponent component, ref PriceCalculationEvent args)
        {
            List<double> priceSets = new();

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

        private void OnTryVocalize(Entity<VendingMachineComponent> ent, ref TryVocalizeEvent args)
        {
            args.Cancelled |= ent.Comp.Broken;
        }

        private void OnAfterUIOpen(EntityUid uid, VendingMachineComponent component, AfterActivatableUIOpenEvent args)
        {
            UpdateVendingMachineInterfaceState(uid, component);
        }
    }
}
