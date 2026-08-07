using Content.Shared._Onyx.Clothing.Components;
using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
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
    [Dependency] private PowerCellSystem _power = default!;
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
        if (args.Handled || !TryGetActive(module.Owner, args.Performer, out var controller, out var framework) ||
            !TryConsume(framework, controller, args.Action, args.Performer))
            return;
        var target = FindHelmet(controller);
        if (target == null)
            return;
        if (!TryComp<PointLightComponent>(target, out var pointLight))
        {
            pointLight = EnsureComp<PointLightComponent>(target.Value);
            EnsureComp<ModModuleOwnedLightComponent>(target.Value).Module = module.Owner;
        }
        else if (!TryComp<ModModuleOwnedLightComponent>(target, out var ownership) || ownership.Module != module.Owner)
            return;
        module.Comp.Enabled = !module.Comp.Enabled;
        _light.SetColor(target.Value, module.Comp.Color, pointLight);
        _light.SetEnergy(target.Value, module.Comp.Energy, pointLight);
        _light.SetRadius(target.Value, module.Comp.Radius, pointLight);
        _light.SetEnabled(target.Value, module.Comp.Enabled, pointLight);
        _actions.SetToggled(args.Action.AsNullable(), module.Comp.Enabled);
        Dirty(module);
        args.Handled = true;
    }

    private void OnLightDeactivated(Entity<ModModuleLightComponent> module, ref ModModuleDeactivatedEvent args) => RemoveLight(module, args.Controller);
    private void OnLightUninstalled(Entity<ModModuleLightComponent> module, ref ModModuleUninstalledEvent args) => RemoveLight(module, args.Controller);

    private void RemoveLight(Entity<ModModuleLightComponent> module, EntityUid controller)
    {
        if (FindHelmet(controller) is not { } helmet ||
            !TryComp<ModModuleOwnedLightComponent>(helmet, out var owned) || owned.Module != module.Owner)
            return;
        RemComp<PointLightComponent>(helmet);
        RemComp<ModModuleOwnedLightComponent>(helmet);
        module.Comp.Enabled = false;
        Dirty(module);
    }

    private void OnTeleport(Entity<ModModuleTeleporterComponent> module, ref ModModuleTeleportEvent args)
    {
        if (args.Handled || module.Comp.Radius < 0 || !TryGetActive(module.Owner, args.Performer, out var controller, out var framework) ||
            !TryConsume(framework, controller, args.Action, args.Performer))
            return;
        var targets = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(Transform(args.Performer).Coordinates, module.Comp.Radius, targets, LookupFlags.Uncontained);
        var performer = args.Performer;
        targets.RemoveWhere(x => x.Owner == performer);
        if (targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("mod-module-teleporter-no-target"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }
        _transform.SwapPositions(args.Performer, _random.Pick(targets).Owner);
        args.Handled = true;
    }

    private void OnDispense(Entity<ModModuleDispenserComponent> module, ref ModModuleDispenseEvent args)
    {
        if (args.Handled || module.Comp.Prototypes.Count == 0 || !TryGetActive(module.Owner, args.Performer, out var controller, out var framework) ||
            !TryConsume(framework, controller, args.Action, args.Performer))
            return;
        var item = Spawn(_random.Pick(module.Comp.Prototypes), Transform(args.Performer).Coordinates);
        _hands.TryPickupAnyHand(args.Performer, item, checkActionBlocker: false);
        args.Handled = true;
    }

    private bool TryGetActive(EntityUid module,
        EntityUid performer,
        out EntityUid controller,
        out ModModuleComponent component)
    {
        controller = default;
        component = default!;
        if (!TryComp<ModModuleComponent>(module, out var found) || !found.Active || found.InstalledController is not { } installed ||
            !TryComp<SealableClothingControlComponent>(installed, out var seal) || seal.WearerEntity != performer)
            return false;
        component = found;
        controller = installed;
        return true;
    }

    private bool TryConsume(ModModuleComponent module, EntityUid controller, Entity<ActionComponent> action, EntityUid performer)
    {
        foreach (var definition in module.Actions)
        {
            if (!module.ActionEntities.TryGetValue(definition.Action, out var actionUid) || actionUid != action.Owner)
                continue;
            return definition.UseCost <= 0 || _power.TryUseCharge(controller, definition.UseCost, performer, predicted: true);
        }
        return false;
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
}
