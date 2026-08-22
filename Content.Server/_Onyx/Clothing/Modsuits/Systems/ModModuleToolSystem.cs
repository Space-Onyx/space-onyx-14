using System.Linq;
using Content.Server.Temperature.Systems;
using Content.Shared._Onyx.Clothing.Components;
using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleToolSystem : EntitySystem
{
    private const string GrabberContainer = "mod-grabber";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _power = default!;
    [Dependency] private TemperatureSystem _temperature = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModModuleHolsterComponent, ModModuleHolsterEvent>(OnHolster);
        SubscribeLocalEvent<ModModuleGrabberToolComponent, MapInitEvent>(OnGrabberInit);
        SubscribeLocalEvent<ModModuleGrabberToolComponent, AfterInteractEvent>(OnGrab);
        SubscribeLocalEvent<ModModuleGrabberToolComponent, ModModuleGrabDoAfterEvent>(OnGrabComplete);
        SubscribeLocalEvent<ModModuleGrabberToolComponent, GetVerbsEvent<AlternativeVerb>>(OnGrabberVerb);
        SubscribeLocalEvent<ModModuleMicrowaveToolComponent, AfterInteractEvent>(OnMicrowave);
        SubscribeLocalEvent<ModModuleMicrowaveToolComponent, ModModuleMicrowaveDoAfterEvent>(OnMicrowaveComplete);
        SubscribeLocalEvent<ModModuleComponent, ModModuleDeactivatedEvent>(OnModuleDeactivated);
        SubscribeLocalEvent<ModModuleComponent, ModModuleUninstalledEvent>(OnModuleUninstalled);
    }

    private void OnHolster(Entity<ModModuleHolsterComponent> module, ref ModModuleHolsterEvent args)
    {
        if (args.Handled || !TryController(module.Owner, args.Performer, out var controller, out var framework) ||
            !_slots.TryGetSlot(module.Owner, module.Comp.ContainerId, out var slot))
            return;
        if (slot.Item is { } stored)
        {
            if (!_hands.TryPickupAnyHand(args.Performer, stored, checkActionBlocker: false)) return;
        }
        else
        {
            var held = _hands.GetActiveItem(args.Performer);
            if (held == null || !_slots.TryInsert(module, slot, held.Value, args.Performer))
            {
                _popup.PopupEntity(Loc.GetString("mod-module-holster-invalid"), args.Performer, args.Performer);
                return;
            }
        }
        _actions.SetToggled(args.Action.AsNullable(), slot.Item != null);
        args.Handled = true;
    }

    private void OnGrabberInit(Entity<ModModuleGrabberToolComponent> tool, ref MapInitEvent args) =>
        _containers.EnsureContainer<Container>(tool, GrabberContainer);

    private void OnGrab(Entity<ModModuleGrabberToolComponent> tool, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !CanGrab(target) ||
            !_containers.TryGetContainer(tool, GrabberContainer, out var container) || container.Count >= tool.Comp.MaxContents ||
            !TryLink(tool.Owner, args.User, ref tool.Comp.Module, out _, out _))
            return;
        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, tool.Comp.Delay,
            new ModModuleGrabDoAfterEvent(), tool, target, tool)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnGrabComplete(Entity<ModModuleGrabberToolComponent> tool, ref ModModuleGrabDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanGrab(target) ||
            !_containers.TryGetContainer(tool, GrabberContainer, out var container) || container.Count >= tool.Comp.MaxContents ||
            !TryLink(tool.Owner, args.User, ref tool.Comp.Module, out var controller, out _) ||
            !_power.TryUseCharge(controller, tool.Comp.UseCost, args.User) || !_containers.Insert(target, container))
            return;
        args.Handled = true;
    }

    private void OnGrabberVerb(Entity<ModModuleGrabberToolComponent> tool, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_containers.TryGetContainer(tool, GrabberContainer, out var container) || container.Count == 0)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("mod-module-grabber-eject"),
            Act = () => Eject(container, Transform(user).Coordinates),
        });
    }

    private void OnMicrowave(Entity<ModModuleMicrowaveToolComponent> tool, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !CanHeat(target) ||
            !TryLink(tool.Owner, args.User, ref tool.Comp.Module, out _, out _))
            return;
        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, tool.Comp.Delay,
            new ModModuleMicrowaveDoAfterEvent(), tool, target, tool)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnMicrowaveComplete(Entity<ModModuleMicrowaveToolComponent> tool, ref ModModuleMicrowaveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanHeat(target) ||
            !TryLink(tool.Owner, args.User, ref tool.Comp.Module, out var controller, out _) ||
            !_power.TryUseCharge(controller, tool.Comp.UseCost, args.User))
            return;
        _temperature.ChangeHeat(target, tool.Comp.Heat);
        args.Handled = true;
    }

    private void OnModuleDeactivated(Entity<ModModuleComponent> module, ref ModModuleDeactivatedEvent args) => CleanupTools(module, args.Controller);
    private void OnModuleUninstalled(Entity<ModModuleComponent> module, ref ModModuleUninstalledEvent args) => CleanupTools(module, args.Controller);

    private void CleanupTools(Entity<ModModuleComponent> module, EntityUid controller)
    {
        foreach (var item in module.Comp.ItemEntities.Values)
            if (Exists(item) && _containers.TryGetContainer(item, GrabberContainer, out var container))
                Eject(container, Transform(controller).Coordinates);
    }

    private bool TryLink(EntityUid tool, EntityUid user, ref EntityUid? module, out EntityUid controller, out ModModuleComponent framework)
    {
        controller = default;
        framework = default!;
        if (module is not { } moduleUid)
        {
            moduleUid = FindOwningModule(tool);
            module = moduleUid.IsValid() ? moduleUid : null;
        }
        return module is { } found && TryController(found, user, out controller, out framework);
    }

    private EntityUid FindOwningModule(EntityUid tool)
    {
        var query = EntityQueryEnumerator<ModModuleComponent>();
        while (query.MoveNext(out var uid, out var module))
            if (module.ItemEntities.Values.Contains(tool)) return uid;
        return EntityUid.Invalid;
    }

    private bool TryController(EntityUid module, EntityUid user, out EntityUid controller, out ModModuleComponent framework)
    {
        controller = default;
        framework = default!;
        if (!TryComp<ModModuleComponent>(module, out var found) || !found.Active || found.InstalledController is not { } installed ||
            !TryComp<SealableClothingControlComponent>(installed, out var seal) || seal.WearerEntity != user)
            return false;
        framework = found;
        controller = installed;
        return true;
    }

    private bool CanGrab(EntityUid target) => !HasComp<MobStateComponent>(target) && !Transform(target).Anchored &&
        (!TryComp<PhysicsComponent>(target, out var physics) || physics.BodyType != BodyType.Static);
    private bool CanHeat(EntityUid target) => !HasComp<MobStateComponent>(target) && HasComp<TemperatureComponent>(target);

    private void Eject(BaseContainer container, EntityCoordinates destination)
    {
        foreach (var item in container.ContainedEntities.ToArray())
            _containers.Remove(item, container, destination: destination);
    }
}
