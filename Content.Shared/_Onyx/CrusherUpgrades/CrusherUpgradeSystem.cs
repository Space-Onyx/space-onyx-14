using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CrusherUpgrades;

public sealed partial class CrusherUpgradeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ItemUpgradeableComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ItemUpgradeableComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<ItemUpgradeableComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<ItemUpgradeableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ItemUpgradeableComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ItemUpgradeableComponent, GetMeleeDamageEvent>(Relay);
        SubscribeLocalEvent<ItemUpgradeableComponent, GetMeleeAttackRateEvent>(Relay);
        SubscribeLocalEvent<ItemUpgradeableComponent, GetMeleeRangeEvent>(Relay);
        SubscribeLocalEvent<ItemUpgradeableComponent, MeleeHitEvent>(Relay);
        SubscribeLocalEvent<CrusherUpgradeComponentsComponent, EntGotInsertedIntoContainerMessage>(OnUpgradeInserted);
        SubscribeLocalEvent<CrusherUpgradeComponentsComponent, EntGotRemovedFromContainerMessage>(OnUpgradeRemoved);
        SubscribeLocalEvent<ItemUpgradeComponent, ExaminedEvent>(OnUpgradeExamine);
        SubscribeLocalEvent<WeaponUpgradeDamageComponent, GetMeleeDamageEvent>(OnDamage);
        SubscribeLocalEvent<WeaponUpgradeSpeedComponent, GetMeleeAttackRateEvent>(OnSpeed);
        SubscribeLocalEvent<WeaponUpgradeRangeComponent, GetMeleeRangeEvent>(OnRange);
    }

    private void OnInsertAttempt(Entity<ItemUpgradeableComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (!TryComp<ItemUpgradeComponent>(args.Item, out var upgrade) || upgrade.UniqueGroup == null)
            return;

        foreach (var current in Upgrades(ent))
        {
            if (current.Comp.UniqueGroup == upgrade.UniqueGroup)
            {
                args.Cancelled = true;
                return;
            }
        }
    }

    private void OnContainerChanged(Entity<ItemUpgradeableComponent> ent, ref EntInsertedIntoContainerMessage args) => RefreshGun(ent);
    private void OnContainerChanged(Entity<ItemUpgradeableComponent> ent, ref EntRemovedFromContainerMessage args) => RefreshGun(ent);

    private void OnMapInit(Entity<ItemUpgradeableComponent> ent, ref MapInitEvent args)
    {
        var ownership = EnsureComp<CrusherUpgradeOwnershipComponent>(ent);
        var previouslyAdded = ownership.AddedComponents.ToArray();
        ownership.References.Clear();

        foreach (var upgrade in Upgrades(ent))
        {
            if (TryComp<CrusherUpgradeComponentsComponent>(upgrade, out var registry))
                Acquire(ent, registry.Components, ownership);
        }

        foreach (var name in previouslyAdded)
        {
            if (ownership.References.ContainsKey(name))
                continue;

            ownership.AddedComponents.Remove(name);
            if (Factory.TryGetRegistration(name, out var registration))
                RemComp(ent.Owner, registration.Type);
        }

        RefreshGun(ent);
    }

    private void RefreshGun(Entity<ItemUpgradeableComponent> ent)
    {
        if (TryComp<GunComponent>(ent, out var gun))
            _gun.RefreshModifiers((ent, gun));
    }

    private void OnExamine(Entity<ItemUpgradeableComponent> ent, ref ExaminedEvent args)
    {
        foreach (var upgrade in Upgrades(ent))
        {
            if (upgrade.Comp.InsertedTextType is { } text)
                args.PushMarkup(Loc.GetString(text, ("name", Loc.GetString(upgrade.Comp.Name))));
        }
    }

    private void OnUpgradeExamine(Entity<ItemUpgradeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.ExamineTextType is { } text)
            args.PushMarkup(Loc.GetString(text, ("name", Loc.GetString(ent.Comp.Name))));
    }

    private void OnUpgradeInserted(Entity<CrusherUpgradeComponentsComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!_timing.ApplyingState && HasComp<ItemUpgradeableComponent>(args.Container.Owner))
            Acquire(args.Container.Owner, ent.Comp.Components);
    }

    private void OnUpgradeRemoved(Entity<CrusherUpgradeComponentsComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (!_timing.ApplyingState && HasComp<ItemUpgradeableComponent>(args.Container.Owner))
            Release(args.Container.Owner, ent.Comp.Components);
    }

    private void Acquire(EntityUid owner,
        ComponentRegistry registry,
        CrusherUpgradeOwnershipComponent? ownership = null)
    {
        ownership ??= EnsureComp<CrusherUpgradeOwnershipComponent>(owner);
        foreach (var (name, entry) in registry)
        {
            ownership.References.TryGetValue(name, out var references);
            ownership.References[name] = references + 1;
            if (references > 0 || HasComp(owner, entry.Component.GetType()))
                continue;

            var single = new ComponentRegistry { [name] = entry };
            EntityManager.AddComponents(owner, single, removeExisting: false);
            ownership.AddedComponents.Add(name);
        }
    }

    private void Release(EntityUid owner, ComponentRegistry registry)
    {
        if (!TryComp<CrusherUpgradeOwnershipComponent>(owner, out var ownership))
            return;

        foreach (var (name, entry) in registry)
        {
            if (!ownership.References.TryGetValue(name, out var references))
                continue;

            if (references > 1)
            {
                ownership.References[name] = references - 1;
                continue;
            }

            ownership.References.Remove(name);
            if (ownership.AddedComponents.Remove(name))
                RemComp(owner, entry.Component.GetType());
        }
    }

    private void Relay<T>(Entity<ItemUpgradeableComponent> ent, ref T args) where T : notnull
    {
        foreach (var upgrade in Upgrades(ent))
            RaiseLocalEvent(upgrade.Owner, ref args);
    }

    private HashSet<Entity<ItemUpgradeComponent>> Upgrades(Entity<ItemUpgradeableComponent> ent)
    {
        var result = new HashSet<Entity<ItemUpgradeComponent>>();
        if (!TryComp<ItemSlotsComponent>(ent, out var slots))
            return result;

        foreach (var slot in slots.Slots.Values)
        {
            if (slot.Item is { } item && TryComp<ItemUpgradeComponent>(item, out var upgrade))
                result.Add((item, upgrade));
        }
        return result;
    }

    private void OnDamage(Entity<WeaponUpgradeDamageComponent> ent, ref GetMeleeDamageEvent args) => args.Damage += ent.Comp.BonusDamage;
    private void OnSpeed(Entity<WeaponUpgradeSpeedComponent> ent, ref GetMeleeAttackRateEvent args) => args.Multipliers *= ent.Comp.AttackRateMultiplier;
    private void OnRange(Entity<WeaponUpgradeRangeComponent> ent, ref GetMeleeRangeEvent args) => args.Multipliers *= ent.Comp.RangeMultiplier;
}
