using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleActionSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ModModuleSystem _modules = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModModuleLightComponent, ModModuleToggleLightEvent>(OnToggleLight);
        SubscribeLocalEvent<ModModuleLightComponent, ModModuleDeactivatedEvent>(OnLightDeactivated);
        SubscribeLocalEvent<ModModuleLightComponent, ModModuleUninstalledEvent>(OnLightUninstalled);
        SubscribeLocalEvent<ModModuleTeleporterComponent, ModModuleTeleportEvent>(OnTeleport);
        SubscribeLocalEvent<ModModuleDispenserComponent, ModModuleDispenseEvent>(OnDispense);
    }

    private void OnToggleLight(Entity<ModModuleLightComponent> module, ref ModModuleToggleLightEvent args)
    {
        if (!args.Handled)
            args.Handled = TryToggleLight(module.AsNullable(), args.Performer, args.Action);
    }

    /// <summary>
    /// Toggles the helmet light after validating its owner and action charge.
    /// </summary>
    public bool TryToggleLight(Entity<ModModuleLightComponent?> module, EntityUid performer, Entity<ActionComponent> action)
    {
        if (!Resolve(module, ref module.Comp, false) || !_modules.CanUseModule(module.Owner, performer, out var controller))
            return false;
        var target = FindHelmet(controller);
        if (target == null)
            return false;
        if (TryComp<ModModuleOwnedLightComponent>(target, out var existingOwner) && existingOwner.Module != module.Owner)
            return false;
        if (!_modules.TryUseAction(module.Owner, performer, action, out _))
            return false;
        if (!TryComp<PointLightComponent>(target, out var pointLight))
        {
            pointLight = EnsureComp<PointLightComponent>(target.Value);
            var added = EnsureComp<ModModuleOwnedLightComponent>(target.Value);
            added.Module = module.Owner;
            added.Added = true;
        }
        else if (!TryComp<ModModuleOwnedLightComponent>(target, out var ownership))
        {
            var saved = EnsureComp<ModModuleOwnedLightComponent>(target.Value);
            saved.Module = module.Owner;
            saved.Enabled = pointLight.Enabled;
            saved.Energy = pointLight.Energy;
            saved.Radius = pointLight.Radius;
            saved.Color = pointLight.Color;
        }
        else if (ownership.Module != module.Owner)
            return false;
        module.Comp.Enabled = !module.Comp.Enabled;
        _light.SetEnergy(target.Value, module.Comp.Energy, pointLight);
        _light.SetRadius(target.Value, module.Comp.Radius, pointLight);
        _light.SetEnabled(target.Value, module.Comp.Enabled, pointLight);
        _light.SetColor(target.Value, module.Comp.Color, pointLight);
        _actions.SetToggled(action.AsNullable(), module.Comp.Enabled);
        Dirty(module);
        return true;
    }

    private void OnLightDeactivated(Entity<ModModuleLightComponent> module, ref ModModuleDeactivatedEvent args) => RemoveLight(module, args.Controller);
    private void OnLightUninstalled(Entity<ModModuleLightComponent> module, ref ModModuleUninstalledEvent args) => RemoveLight(module, args.Controller);

    private void RemoveLight(Entity<ModModuleLightComponent> module, EntityUid controller)
    {
        if (FindHelmet(controller) is not { } helmet ||
            !TryComp<ModModuleOwnedLightComponent>(helmet, out var owned) || owned.Module != module.Owner)
            return;
        if (TryComp<PointLightComponent>(helmet, out var pointLight))
        {
            if (owned.Added)
                RemComp<PointLightComponent>(helmet);
            else
            {
                _light.SetEnabled(helmet, owned.Enabled, pointLight);
                _light.SetEnergy(helmet, owned.Energy, pointLight);
                _light.SetRadius(helmet, owned.Radius, pointLight);
                _light.SetColor(helmet, owned.Color, pointLight);
            }
        }
        RemComp<ModModuleOwnedLightComponent>(helmet);
        module.Comp.Enabled = false;
        if (TryComp<ModModuleComponent>(module, out var framework))
            foreach (var action in framework.ActionEntities.Values)
                _actions.SetToggled(action, false);
        Dirty(module);
    }

    private void OnTeleport(Entity<ModModuleTeleporterComponent> module, ref ModModuleTeleportEvent args)
    {
        if (!args.Handled)
            args.Handled = TryTeleport(module.AsNullable(), args.Performer, args.Action);
    }

    /// <summary>
    /// Exchanges positions with an uncontained mob in range without charging failed searches.
    /// </summary>
    public bool TryTeleport(Entity<ModModuleTeleporterComponent?> module, EntityUid performer, Entity<ActionComponent> action)
    {
        if (!Resolve(module, ref module.Comp, false) || module.Comp.Radius < 0 || !_modules.CanUseModule(module.Owner, performer, out _))
            return false;
        var targets = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(Transform(performer).Coordinates, module.Comp.Radius, targets, LookupFlags.Uncontained);
        targets.RemoveWhere(x => x.Owner == performer);
        if (targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("mod-module-teleporter-no-target"), performer, performer);
            return false;
        }
        if (!_modules.TryUseAction(module.Owner, performer, action, out _))
            return false;
        _transform.SwapPositions(performer, _random.Pick(targets).Owner);
        return true;
    }

    private void OnDispense(Entity<ModModuleDispenserComponent> module, ref ModModuleDispenseEvent args)
    {
        if (!args.Handled)
            args.Handled = TryDispense(module.AsNullable(), args.Performer, args.Action);
    }

    /// <summary>
    /// Creates one configured item after validating and charging the module action.
    /// </summary>
    public bool TryDispense(Entity<ModModuleDispenserComponent?> module, EntityUid performer, Entity<ActionComponent> action)
    {
        if (!Resolve(module, ref module.Comp, false) || module.Comp.Prototypes.Count == 0 ||
            !_modules.TryUseAction(module.Owner, performer, action, out _))
            return false;
        var item = Spawn(_random.Pick(module.Comp.Prototypes), Transform(performer).Coordinates);
        _hands.TryPickupAnyHand(performer, item, checkActionBlocker: false);
        return true;
    }

    private EntityUid? FindHelmet(EntityUid controller)
    {
        if (!TryComp<Content.Shared.Clothing.Components.ToggleableClothingComponent>(controller, out var toggleable))
            return null;
        foreach (var (part, slot) in toggleable.ClothingUids)
            if (slot == "head") return part;
        return null;
    }
}

[RegisterComponent]
public sealed partial class ModModuleOwnedLightComponent : Component
{
    public EntityUid Module;
    public bool Added;
    public bool Enabled;
    public float Energy;
    public float Radius;
    public Color Color;
}
