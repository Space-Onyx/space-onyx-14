using System.Linq;
using Content.Server.Temperature.Systems;
using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleToolSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _power = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private TemperatureSystem _temperature = default!;
    [Dependency] private SharedWieldableSystem _wieldable = default!;
    [Dependency] private ModModuleSystem _modules = default!;

    private const string GrabberContainer = "mod-grabber";

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
        if (!args.Handled)
            args.Handled = TryToggleHolster(module.AsNullable(), args.Performer, args.Action);
    }

    private void OnGrabberInit(Entity<ModModuleGrabberToolComponent> tool, ref MapInitEvent args) =>
        _containers.EnsureContainer<Container>(tool, GrabberContainer);

    private void OnGrab(Entity<ModModuleGrabberToolComponent> tool, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.CanReach && args.Target is { } target)
            args.Handled = TryStartGrab(tool.AsNullable(), args.User, target);
    }

    private void OnGrabComplete(Entity<ModModuleGrabberToolComponent> tool, ref ModModuleGrabDoAfterEvent args)
    {
        if (!args.Cancelled && !args.Handled && args.Target is { } target)
            args.Handled = TryGrab(tool.AsNullable(), args.User, target);
    }

    private void OnGrabberVerb(Entity<ModModuleGrabberToolComponent> tool, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanEject(tool.AsNullable(), args.User, out _, out _))
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("mod-module-grabber-eject"),
            Act = () => TryEject(tool.AsNullable(), user),
        });
    }

    private void OnMicrowave(Entity<ModModuleMicrowaveToolComponent> tool, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.CanReach && args.Target is { } target)
            args.Handled = TryStartHeating(tool.AsNullable(), args.User, target);
    }

    private void OnMicrowaveComplete(Entity<ModModuleMicrowaveToolComponent> tool, ref ModModuleMicrowaveDoAfterEvent args)
    {
        if (!args.Cancelled && !args.Handled && args.Target is { } target)
            args.Handled = TryHeat(tool.AsNullable(), args.User, target);
    }

    private void OnModuleDeactivated(Entity<ModModuleComponent> module, ref ModModuleDeactivatedEvent args) => CleanupTools(module, args.Controller);
    private void OnModuleUninstalled(Entity<ModModuleComponent> module, ref ModModuleUninstalledEvent args) => CleanupTools(module, args.Controller);

    /// <summary>
    /// Stores the held weapon or draws the stored weapon, spending charge only after checking the transfer.
    /// </summary>
    public bool TryToggleHolster(Entity<ModModuleHolsterComponent?> module, EntityUid user, Entity<ActionComponent> action)
    {
        if (!Resolve(module, ref module.Comp, false) || !CanToggleHolster(module, user, out var slot))
            return false;
        if (!_modules.TryUseAction(module.Owner, user, action, out _))
            return false;
        if (slot.Item is { } stored)
        {
            var activeHand = _hands.GetActiveHand(user);
            if (activeHand == null || !_hands.TryPickup(user, stored, activeHand, checkActionBlocker: false))
                return false;
            if (TryComp<WieldableComponent>(stored, out var wieldable))
                _wieldable.TryWield((stored, wieldable), user);
            if (TryComp<ChamberMagazineAmmoProviderComponent>(stored, out var chamber) && chamber.BoltClosed == false)
                _gun.SetBoltClosed(stored, chamber, true, user);
        }
        else
        {
            var held = _hands.GetActiveItem(user);
            if (held == null || !_slots.TryInsert(module, slot, held.Value, user))
                return false;
        }
        _actions.SetToggled(action.AsNullable(), slot.Item != null);
        return true;
    }

    /// <summary>
    /// Checks the active hand and holster whitelist without changing either container.
    /// </summary>
    public bool CanToggleHolster(Entity<ModModuleHolsterComponent?> module, EntityUid user, out ItemSlot slot)
    {
        slot = default!;
        if (!Resolve(module, ref module.Comp, false) || !_modules.CanUseModule(module.Owner, user, out _) ||
            !_slots.TryGetSlot(module.Owner, module.Comp.ContainerId, out var holster))
            return false;
        slot = holster;
        if (slot.Item is { } stored)
        {
            var hand = _hands.GetActiveHand(user);
            return hand != null && _hands.CanPickupToHand(user, stored, hand, checkActionBlocker: false);
        }
        if (_hands.GetActiveItem(user) is { } held && _slots.CanInsert(module, slot, held, user))
            return true;
        _popup.PopupEntity(Loc.GetString("mod-module-holster-invalid"), user, user);
        return false;
    }

    /// <summary>
    /// Starts the grabber delay after checking ownership, capacity and the target.
    /// </summary>
    public bool TryStartGrab(Entity<ModModuleGrabberToolComponent?> tool, EntityUid user, EntityUid target)
    {
        if (!Resolve(tool, ref tool.Comp, false) || !CanGrab(tool, user, target, out _, out _))
            return false;
        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, tool.Comp!.Delay,
            new ModModuleGrabDoAfterEvent(), tool, target, tool)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 0.01f,
        });
    }

    /// <summary>
    /// Rechecks the delayed grab before consuming energy and inserting the target.
    /// </summary>
    public bool TryGrab(Entity<ModModuleGrabberToolComponent?> tool, EntityUid user, EntityUid target)
    {
        if (!Resolve(tool, ref tool.Comp, false) || !CanGrab(tool, user, target, out var container, out var controller))
            return false;
        return _power.TryUseCharge(controller, tool.Comp!.UseCost, user) && _containers.Insert(target, container);
    }

    /// <summary>
    /// Checks a non-living movable target and available grabber capacity.
    /// </summary>
    public bool CanGrab(Entity<ModModuleGrabberToolComponent?> tool, EntityUid user, EntityUid target,
        out BaseContainer container, out EntityUid controller)
    {
        container = default!;
        controller = default;
        if (!Resolve(tool, ref tool.Comp, false) || !Exists(target) || HasComp<MobStateComponent>(target) || Transform(target).Anchored ||
            TryComp<PhysicsComponent>(target, out var physics) && physics.BodyType == BodyType.Static)
            return false;
        if (!_containers.TryGetContainer(tool, GrabberContainer, out var storage))
            return false;
        container = storage;
        return container.Count < tool.Comp.MaxContents && CanUseTool(tool, user, out controller) &&
            _containers.CanInsert(target, container);
    }

    /// <summary>
    /// Starts the microwave delay without spending charge.
    /// </summary>
    public bool TryStartHeating(Entity<ModModuleMicrowaveToolComponent?> tool, EntityUid user, EntityUid target)
    {
        if (!Resolve(tool, ref tool.Comp, false) || !CanHeat(tool, user, target, out _))
            return false;
        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, tool.Comp!.Delay,
            new ModModuleMicrowaveDoAfterEvent(), tool, target, tool)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 0.01f,
        });
    }

    /// <summary>
    /// Applies the configured heat after rechecking the delayed interaction.
    /// </summary>
    public bool TryHeat(Entity<ModModuleMicrowaveToolComponent?> tool, EntityUid user, EntityUid target)
    {
        if (!Resolve(tool, ref tool.Comp, false) || !CanHeat(tool, user, target, out var controller) ||
            !_power.TryUseCharge(controller, tool.Comp.UseCost, user))
            return false;
        _temperature.ChangeHeat(target, tool.Comp!.Heat);
        return true;
    }

    /// <summary>
    /// Restricts microwave heating to non-living temperature-bearing targets.
    /// </summary>
    public bool CanHeat(Entity<ModModuleMicrowaveToolComponent?> tool, EntityUid user, EntityUid target, out EntityUid controller)
    {
        controller = default;
        return Resolve(tool, ref tool.Comp, false) && !HasComp<MobStateComponent>(target) && HasComp<TemperatureComponent>(target) &&
            CanUseTool(tool, user, out controller);
    }

    /// <summary>
    /// Releases grabbed items after validating the current holder and module.
    /// </summary>
    public bool TryEject(Entity<ModModuleGrabberToolComponent?> tool, EntityUid user)
    {
        if (!Resolve(tool, ref tool.Comp, false) || !CanEject(tool, user, out var container, out var controller) ||
            !_power.TryUseCharge(controller, tool.Comp.UseCost, user))
            return false;
        Eject(container, Transform(user).Coordinates);
        return true;
    }

    /// <summary>
    /// Checks that an active, held grabber contains items to eject.
    /// </summary>
    public bool CanEject(Entity<ModModuleGrabberToolComponent?> tool, EntityUid user, out BaseContainer container, out EntityUid controller)
    {
        container = default!;
        controller = default;
        if (!Resolve(tool, ref tool.Comp, false) || !_containers.TryGetContainer(tool, GrabberContainer, out var storage))
            return false;
        container = storage;
        return container.Count > 0 && CanUseTool(tool, user, out controller);
    }

    private void CleanupTools(Entity<ModModuleComponent> module, EntityUid controller)
    {
        foreach (var item in module.Comp.ItemEntities.Values)
            if (Exists(item) && _containers.TryGetContainer(item, GrabberContainer, out var container))
                Eject(container, Transform(controller).Coordinates);
    }

    private bool CanUseTool(EntityUid tool, EntityUid user, out EntityUid controller)
    {
        controller = default;
        if (!_hands.IsHolding(user, tool, out _))
            return false;
        var query = EntityQueryEnumerator<ModModuleComponent>();
        while (query.MoveNext(out var uid, out var module))
            if (module.ItemEntities.Values.Contains(tool))
                return _modules.CanUseModule(new Entity<ModModuleComponent?>(uid, module), user, out controller);
        return false;
    }

    private void Eject(BaseContainer container, EntityCoordinates destination)
    {
        foreach (var item in container.ContainedEntities.ToArray())
            _containers.Remove(item, container, destination: destination);
    }
}
