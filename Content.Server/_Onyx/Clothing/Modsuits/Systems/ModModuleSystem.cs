using System.Linq;
using Content.Shared._Onyx.Clothing.Components;
using Content.Shared._Onyx.Clothing.Modsuits;
using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared._Onyx.Clothing.Systems;
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Power;
using Content.Shared.Radiation.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleSystem : EntitySystem
{
    private const string ItemContainer = "mod-integrated-items";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _power = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModModuleContainerComponent, ComponentInit>(OnContainerInit);
        SubscribeLocalEvent<ModModuleContainerComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ModModuleContainerComponent, ContainerIsRemovingAttemptEvent>(OnRemoveAttempt);
        SubscribeLocalEvent<ModModuleContainerComponent, ComponentShutdown>(OnContainerShutdown);
        SubscribeLocalEvent<ModModuleContainerComponent, ClothingGotEquippedEvent>(OnSuitChanged);
        SubscribeLocalEvent<ModModuleContainerComponent, ClothingGotUnequippedEvent>(OnSuitUnequipped);
        SubscribeLocalEvent<ModModuleContainerComponent, ClothingControlSealCompleteEvent>(OnSealChanged);
        SubscribeLocalEvent<ModModuleContainerComponent, PowerCellChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ModModuleContainerComponent, PowerCellSlotEmptyEvent>(OnPowerLost);
        SubscribeLocalEvent<ModModuleContainerComponent, BatteryStateChangedEvent>(OnBatteryStateChanged);
        SubscribeLocalEvent<ModModuleContainerComponent, RefreshChargeRateEvent>(OnRefreshChargeRate);
        SubscribeLocalEvent<ModModuleContainerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ModModuleContainerComponent, InteractUsingEvent>(OnModuleInteract, before: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<ModModuleComponent, ModModuleInstallDoAfterEvent>(OnInstallDoAfterFinished);
        SubscribeLocalEvent<ModModuleComponent, ModModuleRemoveDoAfterEvent>(OnRemoveDoAfterFinished);
        SubscribeLocalEvent<ModModuleContainerComponent, ModSuitEjectBatteryBuiMessage>(OnEjectBatteryBuiMessage);
        SubscribeLocalEvent<ModModuleContainerComponent, ModSuitRemoveModuleBuiMessage>(OnRemoveModuleBuiMessage);
        SubscribeLocalEvent<ModModuleComponent, EntGotInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<ModModuleComponent, EntGotRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<ModModuleComponent, ComponentShutdown>(OnModuleShutdown);
        SubscribeLocalEvent<ModModuleComponent, ModModuleActionEvent>(OnModuleAction);
    }

    private void OnContainerInit(Entity<ModModuleContainerComponent> ent, ref ComponentInit args)
    {
        _containers.EnsureContainer<Container>(ent, ModModuleContainerComponent.ContainerId);
        if (ent.Comp.MaxModules < 0)
            Log.Error($"{ToPrettyString(ent)} has negative maxModules; its module bay will reject all modules.");
        RefreshPower(ent);
    }

    private void OnInsertAttempt(Entity<ModModuleContainerComponent> controller, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || args.Container.ID != ModModuleContainerComponent.ContainerId)
            return;
        if (!TryComp<ModModuleComponent>(args.EntityUid, out var module) ||
            !CanInstall((args.EntityUid, module), controller, args.AssumeEmpty, out _))
            args.Cancel();
    }

    private void OnRemoveAttempt(Entity<ModModuleContainerComponent> controller, ref ContainerIsRemovingAttemptEvent args)
    {
        if (args.Cancelled || args.Container.ID != ModModuleContainerComponent.ContainerId)
            return;
        if (!TryComp<ModModuleComponent>(args.EntityUid, out var module) || !CanRemove((args.EntityUid, module), controller, out _))
            args.Cancel();
    }

    private void OnContainerShutdown(Entity<ModModuleContainerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var module in Modules(ent).ToArray())
            Deactivate(module, ent);
    }

    private void OnSuitChanged(Entity<ModModuleContainerComponent> ent, ref ClothingGotEquippedEvent args) => Refresh(ent);

    private void OnSuitUnequipped(Entity<ModModuleContainerComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        foreach (var module in Modules(ent))
            Deactivate(module, ent, args.Wearer);
        RefreshDraw(ent);
    }
    private void OnSealChanged(Entity<ModModuleContainerComponent> ent, ref ClothingControlSealCompleteEvent args) => Refresh(ent);

    private void OnPowerChanged(Entity<ModModuleContainerComponent> ent, ref PowerCellChangedEvent args)
    {
        RefreshPower(ent);
        Refresh(ent);
    }

    private void OnPowerLost(Entity<ModModuleContainerComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        ent.Comp.Powered = false;
        Dirty(ent);
        Refresh(ent);
    }

    private void OnBatteryStateChanged(Entity<ModModuleContainerComponent> ent, ref BatteryStateChangedEvent args)
    {
        RefreshPower(ent);
        Refresh(ent);
    }

    private void OnRefreshChargeRate(Entity<ModModuleContainerComponent> ent, ref RefreshChargeRateEvent args)
    {
        args.NewChargeRate -= Modules(ent).Where(x => x.Comp.Active).Sum(x => x.Comp.DrawRate);
    }

    private void OnModuleInteract(Entity<ModModuleContainerComponent> controller, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<ModModuleComponent>(args.Used, out var module))
            return;

        args.Handled = true;
        if (!CanInstall((args.Used, module), controller, false, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), controller, args.User);
            return;
        }

        var installDuration = controller.Comp.InstallDuration;
        _popup.PopupEntity(Loc.GetString("mod-module-install-begin"), args.Used, args.User);

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, installDuration,
            new ModModuleInstallDoAfterEvent(), args.Used, target: controller.Owner, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnInstallDoAfterFinished(Entity<ModModuleComponent> module, ref ModModuleInstallDoAfterEvent args)
    {
        if (args.Cancelled || args.Args.Target == null || args.Args.Used == null)
            return;

        if (!TryComp<ModModuleContainerComponent>(args.Args.Target.Value, out var containerComp))
            return;

        if (!TryInstall((args.Args.Used.Value, module), (args.Args.Target.Value, containerComp), out var reason))
            _popup.PopupEntity(Loc.GetString(reason), args.Args.Target.Value, args.User);
        else
            _popup.PopupEntity(Loc.GetString("mod-module-install-finish"), args.Args.Target.Value, args.User);
    }

    private bool TryInstall(Entity<ModModuleComponent> module,
        Entity<ModModuleContainerComponent> controller,
        out string reason)
    {
        if (!CanInstall(module, controller, false, out reason))
            return false;
        if (!_containers.TryGetContainer(controller, ModModuleContainerComponent.ContainerId, out var container))
            return Fail("mod-module-error-container", out reason);
        return _containers.Insert(module.Owner, container) || Fail("mod-module-error-install", out reason);
    }

    private bool CanInstall(Entity<ModModuleComponent> module,
        Entity<ModModuleContainerComponent> controller,
        bool assumeEmpty,
        out string reason)
    {
        reason = string.Empty;
        if (module.Comp.InstalledController != null)
            return Fail("mod-module-error-installed", out reason);
        if (controller.Comp.MaxModules < 0 || module.Comp.DrawRate < 0 ||
            module.Comp.Actions.Any(action => action.UseCost < 0))
            return Fail("mod-module-error-invalid-config", out reason);
        if (module.Comp.Effects.Values.SelectMany(x => x.Values).Any(x => x.Component is MagbootsComponent) ||
            module.Comp.Effects.Values.SelectMany(x => x.Values).Any(x => x.Component is GeigerComponent) &&
            !HasComp<ModModuleGeigerComponent>(module))
            return Fail("mod-module-error-lifecycle-component", out reason);
        if (TryComp<SealableClothingControlComponent>(controller, out var seal) &&
            (seal.IsCurrentlySealed || seal.IsInProcess))
            return Fail("mod-module-error-sealed", out reason);
        if (TryComp<WiresPanelComponent>(controller, out var panel) && !panel.Open)
            return Fail("mod-module-error-panel", out reason);
        if (!_containers.TryGetContainer(controller, ModModuleContainerComponent.ContainerId, out var container))
            return Fail("mod-module-error-container", out reason);
        if (!assumeEmpty && container.Count >= controller.Comp.MaxModules)
            return Fail("mod-module-error-full", out reason);
        if (module.Comp.RequiredPart is { } required && FindPart(controller, required) == null)
            return Fail("mod-module-error-part", out reason);

        foreach (var installed in assumeEmpty ? Enumerable.Empty<Entity<ModModuleComponent>>() : Modules(controller))
        {
            if (installed.Comp.Id == module.Comp.Id)
                return Fail("mod-module-error-duplicate", out reason);
            if (installed.Comp.ConflictTags.Overlaps(module.Comp.ConflictTags))
                return Fail("mod-module-error-conflict", out reason);
        }
        return true;
    }

    private bool CanRemove(Entity<ModModuleComponent> module,
        Entity<ModModuleContainerComponent> controller,
        out string reason)
    {
        if (module.Comp.Permanent)
            return Fail("mod-module-error-permanent", out reason);
        if (TryComp<SealableClothingControlComponent>(controller, out var seal) &&
            (seal.IsCurrentlySealed || seal.IsInProcess))
            return Fail("mod-module-error-sealed", out reason);
        if (TryComp<WiresPanelComponent>(controller, out var panel) && !panel.Open)
            return Fail("mod-module-error-panel", out reason);
        reason = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string reason)
    {
        reason = message;
        return false;
    }

    private void OnModuleInserted(Entity<ModModuleComponent> module, ref EntGotInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState || args.Container.ID != ModModuleContainerComponent.ContainerId ||
            !TryComp<ModModuleContainerComponent>(args.Container.Owner, out var controller))
            return;

        module.Comp.InstalledController = args.Container.Owner;
        Dirty(module);
        var ev = new ModModuleInstalledEvent(args.Container.Owner);
        RaiseLocalEvent(module, ref ev);
        Refresh((args.Container.Owner, controller));
    }

    private void OnModuleRemoved(Entity<ModModuleComponent> module, ref EntGotRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState || args.Container.ID != ModModuleContainerComponent.ContainerId)
            return;

        var controller = args.Container.Owner;
        if (TryComp<ModModuleContainerComponent>(controller, out var container))
            Deactivate(module, (controller, container));
        var ev = new ModModuleUninstalledEvent(controller);
        RaiseLocalEvent(module, ref ev);
        module.Comp.InstalledController = null;
        Dirty(module);
        if (container != null)
            RefreshDraw((controller, container));
    }

    private void OnModuleShutdown(Entity<ModModuleComponent> module, ref ComponentShutdown args)
    {
        if (module.Comp.InstalledController is { } controller &&
            TryComp<ModModuleContainerComponent>(controller, out var container))
            Deactivate(module, (controller, container));
    }

    private void OnGetVerbs(Entity<ModModuleContainerComponent> controller, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        foreach (var module in Modules(controller))
        {
            var captured = module;
            if (module.Comp.CanBeDisabled && !module.Comp.Permanent &&
                TryComp<SealableClothingControlComponent>(controller, out var seal) && seal.WearerEntity == user)
            {
                var enabled = module.Comp.Enabled;
                var toggleReason = string.Empty;
                var canToggle = enabled;
                if (!enabled)
                    canToggle = CanEnable(controller, user, out toggleReason);
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString(enabled ? "mod-module-verb-disable" : "mod-module-verb-enable", ("module", Name(module))),
                    Disabled = !canToggle,
                    Message = enabled || string.IsNullOrEmpty(toggleReason) ? null : Loc.GetString(toggleReason),
                    Act = () => Toggle(captured, controller, user),
                });
            }
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("mod-module-verb-open-panel"),
            Act = () => _ui.TryOpenUi(controller.Owner, ModSuitUiKey.Key, user),
        });
    }

    private bool CanEnable(Entity<ModModuleContainerComponent> controller, EntityUid user, out string reason)
    {
        if (!TryComp<SealableClothingControlComponent>(controller, out var seal) ||
            seal.WearerEntity != user || !seal.IsCurrentlySealed)
            return Fail("mod-module-error-enable-sealed", out reason);
        if (!controller.Comp.Powered)
            return Fail("mod-module-error-enable-power", out reason);
        reason = string.Empty;
        return true;
    }

    private void Toggle(Entity<ModModuleComponent> module,
        Entity<ModModuleContainerComponent> controller,
        EntityUid user)
    {
        if (!module.Comp.CanBeDisabled || module.Comp.Permanent)
            return;
        if (!module.Comp.Enabled && !CanEnable(controller, user, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), controller, user);
            return;
        }
        module.Comp.Enabled = !module.Comp.Enabled;
        Dirty(module);
        Refresh(controller);
    }

    private void Remove(Entity<ModModuleComponent> module,
        Entity<ModModuleContainerComponent> controller,
        EntityUid user)
    {
        if (!CanRemove(module, controller, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), controller, user);
            return;
        }

        var removeDuration = controller.Comp.RemoveDuration;
        _popup.PopupEntity(Loc.GetString("mod-module-remove-begin"), module, user);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, removeDuration,
            new ModModuleRemoveDoAfterEvent(), module.Owner, target: controller.Owner, used: module.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnRemoveDoAfterFinished(Entity<ModModuleComponent> module, ref ModModuleRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Args.Target == null || args.Args.Used == null)
            return;

        if (!TryComp<ModModuleContainerComponent>(args.Args.Target.Value, out var containerComp))
            return;

        var controller = (args.Args.Target.Value, containerComp);

        if (!CanRemove((module.Owner, module), controller, out _))
            return;

        if (!_containers.TryGetContainer(args.Args.Target.Value, ModModuleContainerComponent.ContainerId, out var container) ||
            !container.Contains(module.Owner))
            return;

        if (!_containers.Remove(module.Owner, container, destination: Transform(args.User).Coordinates))
            _popup.PopupEntity(Loc.GetString("mod-module-error-remove"), args.Args.Target.Value, args.User);
        else
            _popup.PopupEntity(Loc.GetString("mod-module-remove-finish"), args.Args.Target.Value, args.User);
    }

    private void OnEjectBatteryBuiMessage(Entity<ModModuleContainerComponent> controller, ref ModSuitEjectBatteryBuiMessage args)
    {
        if (_power.TryEjectBatteryFromSlot(controller.Owner, out var cell, args.Actor))
            _hands.TryPickupAnyHand(args.Actor, cell.Value);
    }

    private void OnRemoveModuleBuiMessage(Entity<ModModuleContainerComponent> controller, ref ModSuitRemoveModuleBuiMessage args)
    {
        var module = GetEntity(args.Module);
        if (!TryComp<ModModuleComponent>(module, out var mod))
            return;

        if (!CanRemove((module, mod), controller, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), controller, args.Actor);
            return;
        }

        var removeDuration = controller.Comp.RemoveDuration;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.Actor, removeDuration,
            new ModModuleRemoveDoAfterEvent(), module, target: controller.Owner, used: module)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void Refresh(Entity<ModModuleContainerComponent> controller)
    {
        EntityUid? wearer = null;
        var active = TryComp<SealableClothingControlComponent>(controller, out var seal) &&
            (wearer = seal.WearerEntity) != null && seal.IsCurrentlySealed && controller.Comp.Powered;
        foreach (var module in Modules(controller))
        {
            if (active && module.Comp.Enabled && wearer is { } user)
                Activate(module, controller, user);
            else
                Deactivate(module, controller);
        }
        RefreshDraw(controller);
    }

    private void Activate(Entity<ModModuleComponent> module,
        Entity<ModModuleContainerComponent> controller,
        EntityUid wearer)
    {
        if (module.Comp.Active)
            Deactivate(module, controller, wearer);
        var applied = new List<(EntityUid Target, ComponentRegistry Registry)>();
        foreach (var (targetKind, registry) in module.Comp.Effects)
        {
            if (FindTarget(controller, wearer, targetKind) is { } target)
            {
                Acquire(target, registry);
                if (HasComp<ModModuleGeigerComponent>(module) && registry.Values.Any(x => x.Component is GeigerComponent))
                {
                    var equipped = new GotEquippedEvent(wearer, target, new SlotDefinition());
                    RaiseLocalEvent(target, equipped);
                }
                applied.Add((target, registry));
                module.Comp.AppliedTargets[targetKind] = target;
            }
        }
        var addedActions = true;
        foreach (var action in module.Comp.Actions)
        {
            module.Comp.ActionEntities.TryGetValue(action.Action, out var stored);
            EntityUid? actionUid = stored.IsValid() ? stored : null;
            if (!_actions.AddAction(wearer, ref actionUid, action.Action, module) || actionUid is not { } uid)
            {
                addedActions = false;
                break;
            }
            module.Comp.ActionEntities[action.Action] = uid;
        }
        if (!addedActions || !ProvideItems(module, wearer))
        {
            _actions.RemoveProvidedActions(wearer, module);
            StoreItems(module, wearer);
            foreach (var (target, registry) in applied)
                Release(target, registry);
            module.Comp.AppliedTargets.Clear();
            module.Comp.Active = false;
            Dirty(module);
            return;
        }
        module.Comp.Active = true;
        Dirty(module);
        var ev = new ModModuleActivatedEvent(controller, wearer);
        RaiseLocalEvent(module, ref ev);
    }

    private void Deactivate(Entity<ModModuleComponent> module,
        Entity<ModModuleContainerComponent> controller,
        EntityUid? knownWearer = null)
    {
        if (!module.Comp.Active)
            return;
        EntityUid? wearer = knownWearer;
        if (TryComp<SealableClothingControlComponent>(controller, out var seal))
            wearer ??= seal.WearerEntity;
        if (wearer is { } user)
            _actions.RemoveProvidedActions(user, module);
        foreach (var (targetKind, registry) in module.Comp.Effects)
        {
            if (module.Comp.AppliedTargets.TryGetValue(targetKind, out var target) && Exists(target))
            {
                if (wearer is { } geigerUser && HasComp<ModModuleGeigerComponent>(module) &&
                    registry.Values.Any(x => x.Component is GeigerComponent))
                {
                    var unequipped = new GotUnequippedEvent(geigerUser, target, new SlotDefinition());
                    RaiseLocalEvent(target, unequipped);
                }
                Release(target, registry);
            }
        }
        module.Comp.AppliedTargets.Clear();
        StoreItems(module, wearer);
        module.Comp.Active = false;
        Dirty(module);
        var ev = new ModModuleDeactivatedEvent(controller, wearer);
        RaiseLocalEvent(module, ref ev);
    }

    private void OnModuleAction(Entity<ModModuleComponent> module, ref ModModuleActionEvent args)
    {
        if (args.Handled || !module.Comp.Active || module.Comp.InstalledController is not { } controller ||
            !TryComp<SealableClothingControlComponent>(controller, out var seal) || seal.WearerEntity != args.Performer)
            return;
        var usedAction = args.Action.Owner;
        var definition = module.Comp.Actions.FirstOrDefault(x =>
            module.Comp.ActionEntities.TryGetValue(x.Action, out var action) && action == usedAction);
        if (definition == null)
            return;
        args.Handled = true;
        var ev = new ModModuleUsedEvent(controller, args.Performer, args.Performer);
        RaiseLocalEvent(module, ref ev);
    }

    private void Acquire(EntityUid target, ComponentRegistry registry)
    {
        var ownership = EnsureComp<ModModuleEffectOwnershipComponent>(target);
        foreach (var (_, entry) in registry)
        {
            var type = entry.Component.GetType();
            var name = Factory.GetComponentName(type);
            ownership.References.TryGetValue(name, out var count);
            ownership.References[name] = count + 1;
            if (count > 0 || HasComp(target, entry.Component.GetType()))
                continue;
            EntityManager.AddComponents(target, new ComponentRegistry { [name] = entry }, removeExisting: false);
            ownership.Added.Add(name);
        }
    }

    private void Release(EntityUid target, ComponentRegistry registry)
    {
        if (!TryComp<ModModuleEffectOwnershipComponent>(target, out var ownership))
            return;
        foreach (var (_, entry) in registry)
        {
            var name = Factory.GetComponentName(entry.Component.GetType());
            if (!ownership.References.TryGetValue(name, out var count))
                continue;
            if (count > 1)
            {
                ownership.References[name] = count - 1;
                continue;
            }
            ownership.References.Remove(name);
            if (ownership.Added.Remove(name))
                RemComp(target, entry.Component.GetType());
        }
        if (ownership.References.Count == 0)
            RemComp<ModModuleEffectOwnershipComponent>(target);
    }

    private bool ProvideItems(Entity<ModModuleComponent> module, EntityUid wearer)
    {
        if (!TryComp<HandsComponent>(wearer, out var hands))
            return module.Comp.IntegratedItems.Count == 0;
        var storage = _containers.EnsureContainer<Container>(module, ItemContainer);
        var created = new List<EntityUid>();
        for (var i = 0; i < module.Comp.IntegratedItems.Count; i++)
        {
            EntityUid item;
            if (module.Comp.ItemEntities.TryGetValue(i, out var existing) && Exists(existing))
                item = existing;
            else
            {
                item = Spawn(module.Comp.IntegratedItems[i].Item, Transform(module).Coordinates);
                module.Comp.ItemEntities[i] = item;
                created.Add(item);
            }
            if (!storage.Contains(item) && !_containers.Insert(item, storage))
            {
                foreach (var uid in created)
                    if (Exists(uid)) QueueDel(uid);
                return false;
            }
        }
        for (var i = 0; i < module.Comp.IntegratedItems.Count; i++)
            _hands.AddHand((wearer, hands), HandId(module, i), module.Comp.IntegratedItems[i].Hand);
        for (var i = 0; i < module.Comp.IntegratedItems.Count; i++)
        {
            var hand = HandId(module, i);
            var item = module.Comp.ItemEntities[i];
            if (!_containers.Remove(item, storage) ||
                !_hands.TryPickup(wearer, item, hand, checkActionBlocker: false, animate: false, handsComp: hands))
            {
                StoreItems(module, wearer);
                return false;
            }
            if (!HasComp<UnremoveableComponent>(item))
            {
                EnsureComp<UnremoveableComponent>(item);
                module.Comp.OwnedUnremoveable.Add(i);
            }
        }
        Dirty(module);
        return true;
    }

    private void StoreItems(Entity<ModModuleComponent> module, EntityUid? wearer)
    {
        var storage = _containers.EnsureContainer<Container>(module, ItemContainer);
        for (var i = 0; i < module.Comp.IntegratedItems.Count; i++)
        {
            var hand = HandId(module, i);
            if (module.Comp.ItemEntities.TryGetValue(i, out var item) && Exists(item))
            {
                if (module.Comp.OwnedUnremoveable.Remove(i))
                    RemComp<UnremoveableComponent>(item);
                if (!storage.Contains(item))
                    _containers.Insert(item, storage);
            }
            if (wearer is { } user && TryComp<HandsComponent>(user, out var hands))
                _hands.RemoveHand((user, hands), hand);
        }
        Dirty(module);
    }

    private static string HandId(EntityUid module, int index) => $"mod-{module.Id}-hand-{index}";

    private void RefreshPower(Entity<ModModuleContainerComponent> controller)
    {
        TryComp<PowerCellDrawComponent>(controller, out var draw);
        TryComp<PowerCellSlotComponent>(controller, out var slot);
        controller.Comp.Powered = slot == null || _power.HasDrawCharge((controller.Owner, draw, slot));
        Dirty(controller);
    }

    private void RefreshDraw(Entity<ModModuleContainerComponent> controller)
    {
        if (!TryComp<PowerCellDrawComponent>(controller, out var draw))
            return;
        _power.SetDrawEnabled((controller.Owner, draw),
            TryComp<SealableClothingControlComponent>(controller, out var seal) && seal.IsCurrentlySealed);
    }

    private EntityUid? FindTarget(EntityUid controller, EntityUid? wearer, ModEffectTarget target) => target switch
    {
        ModEffectTarget.Controller => controller,
        ModEffectTarget.Wearer => wearer,
        ModEffectTarget.Helmet => FindPart(controller, ModPart.Helmet),
        ModEffectTarget.Torso => FindPart(controller, ModPart.Torso),
        ModEffectTarget.Gloves => FindPart(controller, ModPart.Gloves),
        ModEffectTarget.Boots => FindPart(controller, ModPart.Boots),
        _ => null,
    };

    private EntityUid? FindPart(EntityUid controller, ModPart part)
    {
        if (!TryComp<ToggleableClothingComponent>(controller, out var toggleable))
            return null;
        var slot = part switch
        {
            ModPart.Helmet => "head",
            ModPart.Torso => "outerClothing",
            ModPart.Gloves => "gloves",
            ModPart.Boots => "shoes",
            _ => string.Empty,
        };
        return toggleable.ClothingUids.FirstOrDefault(x => x.Value == slot).Key is { Valid: true } uid ? uid : null;
    }

    private IEnumerable<Entity<ModModuleComponent>> Modules(EntityUid controller)
    {
        if (!_containers.TryGetContainer(controller, ModModuleContainerComponent.ContainerId, out var container))
            yield break;
        foreach (var uid in container.ContainedEntities)
        {
            if (TryComp<ModModuleComponent>(uid, out var module))
                yield return (uid, module);
        }
    }
}
