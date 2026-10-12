using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;

namespace Content.Server._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleWearerSystem : EntitySystem
{
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModModuleAntiGravityComponent, ModModuleActivatedEvent>(OnAntiGravityActivated);
        SubscribeLocalEvent<ModModuleAntiGravityComponent, ModModuleDeactivatedEvent>(OnAntiGravityDeactivated);
        SubscribeLocalEvent<ModModuleApparatusComponent, ModModuleActivatedEvent>(OnApparatusActivated);
        SubscribeLocalEvent<ModModuleApparatusComponent, ModModuleDeactivatedEvent>(OnApparatusDeactivated);
        SubscribeLocalEvent<ModModuleQuickCarryComponent, ModModuleActivatedEvent>(OnCarryActivated);
        SubscribeLocalEvent<ModModuleQuickCarryComponent, ModModuleDeactivatedEvent>(OnCarryDeactivated);
        SubscribeLocalEvent<ModModuleArmorBoosterEffectComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficients,
            after: [typeof(SharedArmorSystem)]);
        SubscribeLocalEvent<ModModuleArmorBoosterEffectComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamage,
            after: [typeof(SharedArmorSystem)]);
        SubscribeLocalEvent<ModModuleArmorBoosterEffectComponent, InventoryRelayedEvent<PartDamageModifyEvent>>(OnPartDamage,
            after: [typeof(SharedArmorSystem)]);
        SubscribeLocalEvent<ModModuleArmorBoosterComponent, ModModuleActivatedEvent>(OnArmorActivated);
        SubscribeLocalEvent<ModModuleArmorBoosterComponent, ModModuleDeactivatedEvent>(OnArmorDeactivated);
    }

    private void OnAntiGravityActivated(Entity<ModModuleAntiGravityComponent> module, ref ModModuleActivatedEvent args)
    {
        EnsureComp<ModModuleAntiGravityEffectComponent>(args.Wearer).Module = module.Owner;
        _gravity.RefreshWeightless(args.Wearer);
    }

    private void OnAntiGravityDeactivated(Entity<ModModuleAntiGravityComponent> module, ref ModModuleDeactivatedEvent args)
    {
        if (args.Wearer is { } wearer && TryComp<ModModuleAntiGravityEffectComponent>(wearer, out var effect) && effect.Module == module.Owner)
            RemComp<ModModuleAntiGravityEffectComponent>(wearer);
    }

    private void OnApparatusActivated(Entity<ModModuleApparatusComponent> module, ref ModModuleActivatedEvent args)
    {
        var helmet = FindHelmet(args.Controller);
        if (!TryComp<IngestionBlockerComponent>(helmet, out var blocker) || HasComp<IngestionBlockerComponent>(module))
            return;
        CopyComp(helmet, module, blocker);
        RemComp<IngestionBlockerComponent>(helmet);
    }

    private void OnApparatusDeactivated(Entity<ModModuleApparatusComponent> module, ref ModModuleDeactivatedEvent args)
    {
        if (!TryComp<IngestionBlockerComponent>(module, out var saved))
            return;
        var helmet = FindHelmet(args.Controller);
        if (!HasComp<IngestionBlockerComponent>(helmet))
            CopyComp(module, helmet, saved);
        RemComp<IngestionBlockerComponent>(module);
    }

    private void OnCarryActivated(Entity<ModModuleQuickCarryComponent> module, ref ModModuleActivatedEvent args)
    {
        var effect = EnsureComp<ModModuleQuickCarryEffectComponent>(args.Wearer);
        effect.Module = module.Owner;
        effect.Multiplier = module.Comp.Multiplier;
        Dirty(args.Wearer, effect);
        _speed.RefreshMovementSpeedModifiers(args.Wearer);
    }

    private void OnCarryDeactivated(Entity<ModModuleQuickCarryComponent> module, ref ModModuleDeactivatedEvent args)
    {
        if (args.Wearer is { } wearer && TryComp<ModModuleQuickCarryEffectComponent>(wearer, out var effect) && effect.Module == module.Owner)
            RemComp<ModModuleQuickCarryEffectComponent>(wearer);
        if (args.Wearer is { } user) _speed.RefreshMovementSpeedModifiers(user);
    }

    private void OnArmorActivated(Entity<ModModuleArmorBoosterComponent> module, ref ModModuleActivatedEvent args)
    {
        var effect = EnsureComp<ModModuleArmorBoosterEffectComponent>(args.Controller);
        effect.Module = module.Owner;
        effect.Modifiers = module.Comp.Modifiers;
    }

    private void OnArmorDeactivated(Entity<ModModuleArmorBoosterComponent> module, ref ModModuleDeactivatedEvent args)
    {
        if (TryComp<ModModuleArmorBoosterEffectComponent>(args.Controller, out var effect) && effect.Module == module.Owner)
            RemComp<ModModuleArmorBoosterEffectComponent>(args.Controller);
    }

    private void OnCoefficients(Entity<ModModuleArmorBoosterEffectComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        foreach (var (type, value) in ent.Comp.Modifiers.Coefficients)
            args.Args.DamageModifiers.Coefficients[type] = args.Args.DamageModifiers.Coefficients.GetValueOrDefault(type, 1f) * value;
    }

    private void OnDamage(Entity<ModModuleArmorBoosterEffectComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (!TryComp<WoundHostComponent>(args.Owner, out var host))
        {
            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, ent.Comp.Modifiers);
            return;
        }

        var systemic = new DamageSpecifier(args.Args.Damage);
        foreach (var type in host.LocalizedDamageTypes)
            systemic.DamageDict.Remove(type);
        var reduced = DamageSpecifier.ApplyModifierSet(systemic, ent.Comp.Modifiers);
        foreach (var (type, value) in reduced.DamageDict)
            args.Args.Damage.DamageDict[type] = value;
    }

    private void OnPartDamage(Entity<ModModuleArmorBoosterEffectComponent> ent, ref InventoryRelayedEvent<PartDamageModifyEvent> args) =>
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, ent.Comp.Modifiers);

    private EntityUid FindHelmet(EntityUid controller) => FindPart(controller, "head");
    private EntityUid FindPart(EntityUid controller, string slot)
    {
        if (TryComp<Content.Shared.Clothing.Components.ToggleableClothingComponent>(controller, out var toggleable))
            foreach (var (part, partSlot) in toggleable.ClothingUids)
                if (partSlot == slot) return part;
        return controller;
    }
}

[RegisterComponent] public sealed partial class ModModuleArmorBoosterEffectComponent : Component { public EntityUid Module; public DamageModifierSet Modifiers = new(); }
