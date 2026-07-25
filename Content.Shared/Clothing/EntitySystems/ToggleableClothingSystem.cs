using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Strip;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared.UserInterface;

namespace Content.Shared.Clothing.EntitySystems;

public sealed partial class ToggleableClothingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStrippableSystem _strippable = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableClothingComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ToggleableClothingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ToggleableClothingComponent, ToggleClothingEvent>(OnToggleClothing);
        SubscribeLocalEvent<ToggleableClothingComponent, ToggleableClothingUiMessage>(OnToggleClothingMessage);
        SubscribeLocalEvent<ToggleableClothingComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ToggleableClothingComponent, ComponentRemove>(OnRemoveToggleable);
        SubscribeLocalEvent<ToggleableClothingComponent, GotUnequippedEvent>(OnToggleableUnequip);
        SubscribeLocalEvent<ToggleableClothingComponent, BeingUnequippedAttemptEvent>(OnToggleableUnequipAttempt); // <Onyx-Modsuit-edited>

        SubscribeLocalEvent<AttachedClothingComponent, ComponentInit>(OnAttachedInit);
        SubscribeLocalEvent<AttachedClothingComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<AttachedClothingComponent, GotUnequippedEvent>(OnAttachedUnequip);
        SubscribeLocalEvent<AttachedClothingComponent, ComponentRemove>(OnRemoveAttached);
        SubscribeLocalEvent<AttachedClothingComponent, BeingUnequippedAttemptEvent>(OnAttachedUnequipAttempt);
        SubscribeLocalEvent<AttachedClothingComponent, AttachClothingDoAfterEvent>(OnAttachedDoAfterComplete); // <Onyx-Modsuit-edited>

        SubscribeLocalEvent<ToggleableClothingComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(GetRelayedVerbs);
        SubscribeLocalEvent<ToggleableClothingComponent, GetVerbsEvent<EquipmentVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AttachedClothingComponent, GetVerbsEvent<EquipmentVerb>>(OnGetAttachedStripVerbsEvent);
        SubscribeLocalEvent<ToggleableClothingComponent, ToggleClothingDoAfterEvent>(OnDoAfterComplete);
    }

    private void GetRelayedVerbs(EntityUid uid, ToggleableClothingComponent component, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        OnGetVerbs(uid, component, args.Args);
    }

    private void OnGetVerbs(EntityUid uid, ToggleableClothingComponent component, GetVerbsEvent<EquipmentVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || component.ClothingUid == null || component.Container == null)
            return;

        if (!_inventorySystem.InSlotWithFlags(uid, component.RequiredFlags))
            return;

        var wearer = Transform(uid).ParentUid;
        if (args.User != wearer && component.StripDelay == null)
            return;

        var verb = new EquipmentVerb()
        {
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Text = Loc.GetString(component.VerbText),
        };

        if (args.User == wearer)
        {
            verb.EventTarget = uid;
            verb.ExecutionEventArgs = new ToggleClothingEvent() { Performer = args.User };
        }
        else
        {
            verb.Act = () => StartDoAfter(args.User, uid, Transform(uid).ParentUid, component);
        }

        args.Verbs.Add(verb);
    }

    private void StartDoAfter(EntityUid user, EntityUid item, EntityUid wearer, ToggleableClothingComponent component)
    {
        if (component.StripDelay == null)
            return;

        var (time, stealth) = _strippable.GetStripTimeModifiers(user, wearer, item, component.StripDelay.Value);

        var args = new DoAfterArgs(EntityManager, user, time, new ToggleClothingDoAfterEvent(), item, wearer, item)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            // This should just re-use the BUI range checks & cancel the do after if the BUI closes. But that is all
            // server-side at the moment.
            // TODO BUI REFACTOR.
            DistanceThreshold = 2,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        if (!stealth)
        {
            var popup = Loc.GetString("strippable-component-alert-owner-interact", ("user", Identity.Entity(user, EntityManager)), ("item", item));
            _popupSystem.PopupEntity(popup, wearer, wearer, PopupType.Large);
        }
    }

    private void OnGetAttachedStripVerbsEvent(EntityUid uid, AttachedClothingComponent component, GetVerbsEvent<EquipmentVerb> args)
    {
        // redirect to the attached entity.
        OnGetVerbs(component.AttachedUid, Comp<ToggleableClothingComponent>(component.AttachedUid), args);
    }

    private void OnDoAfterComplete(EntityUid uid, ToggleableClothingComponent component, ToggleClothingDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        ToggleClothing(args.User, uid, component);
    }

    private void OnInteractHand(EntityUid uid, AttachedClothingComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(component.AttachedUid, out ToggleableClothingComponent? toggleCom)
            || toggleCom.Container == null)
            return;

        if (!toggleCom.ClothingUids.TryGetValue(uid, out var slot))
            slot = toggleCom.Slot;

        if (!_inventorySystem.TryUnequip(Transform(uid).ParentUid, slot, force: true))
            return;
        args.Handled = true;
    }

    /// <summary>
    ///     Called when the suit is unequipped, to ensure that the helmet also gets unequipped.
    /// </summary>
    private void OnToggleableUnequip(EntityUid uid, ToggleableClothingComponent component, GotUnequippedEvent args)
    {
        // If it's a part of PVS departure then don't handle it.
        if (_timing.ApplyingState)
            return;

        // <Onyx-Modsuit>
        if (component.Container != null && component.ClothingUids.Count > 0)
        {
            var affected = new List<(EntityUid Part, string Slot)>();
            foreach (var (part, slot) in component.ClothingUids)
            {
                if (component.Container.Contains(part))
                    continue;

                _inventorySystem.TryUnequip(args.EquipTarget, slot, force: true, triggerHandContact: true);
                if (TryComp(part, out AttachedClothingComponent? attached) &&
                    attached.ReplacedClothing?.ContainedEntity is { } stored)
                    _inventorySystem.TryEquip(args.EquipTarget, args.EquipTarget, stored, slot, force: true,
                        triggerHandContact: true, silent: true);

                _containerSystem.Insert(part, component.Container);
                affected.Add((part, slot));
            }

            if (affected.Count > 0)
            {
                var ev = new ToggledBackClothingFullUnequipAndInsertedEvent(uid, args.EquipTarget, affected);
                RaiseLocalEvent(uid, ref ev);
            }
            return;
        }
        // </Onyx-Modsuit>

        // If the attached clothing is not currently in the container, this just assumes that it is currently equipped.
        // This should maybe double check that the entity currently in the slot is actually the attached clothing, but
        // if its not, then something else has gone wrong already...
        if (component.Container != null && component.Container.Count == 0 && component.ClothingUid != null)
            _inventorySystem.TryUnequip(args.EquipTarget, component.Slot, force: true, triggerHandContact: true);
    }

    private void OnRemoveToggleable(EntityUid uid, ToggleableClothingComponent component, ComponentRemove args)
    {
        // If the parent/owner component of the attached clothing is being removed (entity getting deleted?) we will
        // delete the attached entity. We do this regardless of whether or not the attached entity is currently
        // "outside" of the container or not. This means that if a hardsuit takes too much damage, the helmet will also
        // automatically be deleted.

        _actionsSystem.RemoveAction(component.ActionEntity);

        if (component.ClothingUid != null && !_netMan.IsClient)
            QueueDel(component.ClothingUid.Value);
    }

    private void OnAttachedUnequipAttempt(EntityUid uid, AttachedClothingComponent component, BeingUnequippedAttemptEvent args)
    {
        // <Onyx-Modsuit>
        var ev = new OnAttachedUnequipAttemptEvent(component.AttachedUid, args.Equipment, args.UnEquipTarget, false);
        RaiseLocalEvent(args.Equipment, ev);
        if (ev.Cancelled)
        {
            args.Cancel();
            return;
        }
        // </Onyx-Modsuit>

        if (!TryComp(component.AttachedUid, out ToggleableClothingComponent? toggleable) ||
            !toggleable.ClothingUids.TryGetValue(uid, out var slot))
        {
            args.Cancel();
            return;
        }

        if (args.User == args.UnEquipTarget)
            return;

        StartAttachedDoAfter(args.User, uid, component, args.UnEquipTarget, toggleable, slot);
        args.Cancel();
    }

    private void OnRemoveAttached(EntityUid uid, AttachedClothingComponent component, ComponentRemove args)
    {
        // if the attached component is being removed (maybe entity is being deleted?) we will just remove the
        // toggleable clothing component. This means if you had a hard-suit helmet that took too much damage, you would
        // still be left with a suit that was simply missing a helmet. There is currently no way to fix a partially
        // broken suit like this.

        if (!TryComp(component.AttachedUid, out ToggleableClothingComponent? toggleComp))
            return;

        if (toggleComp.LifeStage > ComponentLifeStage.Running)
            return;

        if (!toggleComp.ClothingUids.Remove(uid))
            return;

        if (toggleComp.ClothingUid == uid)
            toggleComp.ClothingUid = toggleComp.ClothingUids.Count > 0 ? toggleComp.ClothingUids.Keys.First() : null;

        Dirty(component.AttachedUid, toggleComp);
        if (toggleComp.ClothingUids.Count > 0)
            return;

        _actionsSystem.RemoveAction(toggleComp.ActionEntity);
        RemComp(component.AttachedUid, toggleComp);
    }

    /// <summary>
    ///     Called if the helmet was unequipped, to ensure that it gets moved into the suit's container.
    /// </summary>
    private void OnAttachedUnequip(EntityUid uid, AttachedClothingComponent component, GotUnequippedEvent args)
    {
        // Let containers worry about it.
        if (_timing.ApplyingState)
            return;

        if (component.LifeStage > ComponentLifeStage.Running)
            return;

        if (!TryComp(component.AttachedUid, out ToggleableClothingComponent? toggleComp))
            return;

        if (LifeStage(component.AttachedUid) > EntityLifeStage.MapInitialized)
            return;

        // As unequipped gets called in the middle of container removal, we cannot call a container-insert without causing issues.
        // So we delay it and process it during a system update:
        if ((toggleComp.ClothingUid != null || toggleComp.ClothingUids.ContainsKey(uid)) && toggleComp.Container != null)
        {
            // <Onyx-Modsuit>
            var ev = new OnToggleableUnequipAttemptEvent(component.AttachedUid, uid, args.EquipTarget, false);
            RaiseLocalEvent(component.AttachedUid, ev);
            if (ev.Cancelled)
                return;
            // </Onyx-Modsuit>

            if (toggleComp.ClothingUids.TryGetValue(uid, out var slot))
            {
                if (component.ReplacedClothing?.ContainedEntity is { } stored)
                    _inventorySystem.TryEquip(args.EquipTarget, args.EquipTarget, stored, slot, force: true,
                        triggerHandContact: true, silent: true);

                _containerSystem.Insert(uid, toggleComp.Container);
                return;
            }

            _containerSystem.Insert(uid, toggleComp.Container);
        }
    }

    /// <summary>
    ///     Equip or unequip the toggleable clothing.
    /// </summary>
    private void OnToggleClothing(EntityUid uid, ToggleableClothingComponent component, ToggleClothingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (component.ClothingUids.Count > 1)
        {
            _ui.OpenUi(uid, ToggleClothingUiKey.Key, args.Performer);
            return;
        }

        if (component.ClothingUids.Count == 1)
        {
            var attached = component.ClothingUids.Keys.First();
            ToggleAttachedClothing(args.Performer, (uid, component), attached);
            return;
        }

        ToggleClothing(args.Performer, uid, component);
    }

    private void OnToggleClothingMessage(Entity<ToggleableClothingComponent> entity,
        ref ToggleableClothingUiMessage args)
    {
        var attached = GetEntity(args.AttachedClothingUid);
        ToggleAttachedClothing(args.Actor, entity, attached);
    }

    private void ToggleAttachedClothing(EntityUid user, Entity<ToggleableClothingComponent> toggleable, EntityUid attached)
    {
        var component = toggleable.Comp;
        if (component.Container == null || !component.ClothingUids.TryGetValue(attached, out var slot))
            return;

        var attempt = new ToggleClothingAttemptEvent(user, toggleable, false);
        RaiseLocalEvent(toggleable, attempt);
        if (attempt.Cancelled)
            return;

        var wearer = Transform(toggleable).ParentUid;
        var suitStorage = FindSuitStorage(wearer);
        if (component.Container.Contains(attached))
        {
            if (_inventorySystem.TryGetSlotEntity(wearer, slot, out var current))
            {
                if (!component.ReplaceCurrentClothing
                    || !TryComp(attached, out AttachedClothingComponent? attachedComp)
                    || attachedComp.ReplacedClothing == null
                    || attachedComp.ReplacedClothing.ContainedEntity != null)
                {
                    _popupSystem.PopupEntity(Loc.GetString("toggleable-clothing-remove-first", ("entity", current)), user, user);
                    return;
                }

                if (!_inventorySystem.TryUnequip(wearer, slot, force: true))
                    return;

                if (!_containerSystem.Insert(current.Value, attachedComp.ReplacedClothing))
                {
                    _inventorySystem.TryEquip(user, wearer, current.Value, slot, force: true,
                        triggerHandContact: true, silent: true);
                    RestoreSuitStorage(wearer, suitStorage);
                    return;
                }
            }

            if (!_inventorySystem.TryEquip(user, wearer, attached, slot, triggerHandContact: true) &&
                TryComp(attached, out AttachedClothingComponent? failedAttached) &&
                failedAttached.ReplacedClothing?.ContainedEntity is { } replaced)
            {
                _containerSystem.Remove(replaced, failedAttached.ReplacedClothing);
                _inventorySystem.TryEquip(user, wearer, replaced, slot, force: true,
                    triggerHandContact: true, silent: true);
            }
            RestoreSuitStorage(wearer, suitStorage);
            return;
        }

        _inventorySystem.TryUnequip(wearer, slot, force: true);
        RestoreSuitStorage(wearer, suitStorage);
    }

    private void ToggleClothing(EntityUid user, EntityUid target, ToggleableClothingComponent component)
    {
        // <Onyx-Modsuit>
        var attempt = new ToggleClothingAttemptEvent(user, target, false);
        RaiseLocalEvent(target, attempt);
        if (attempt.Cancelled)
            return;
        // </Onyx-Modsuit>

        // <Onyx-Modsuit>
        if (component.Container == null || (component.ClothingUids.Count == 0 && component.ClothingUid == null))
            return;

        if (component.ClothingUids.Count == 0 && component.ClothingUid is { } legacyClothing)
        {
            var legacyParent = Transform(target).ParentUid;
            if (component.Container.Count == 0)
                _inventorySystem.TryUnequip(user, legacyParent, component.Slot, force: true);
            else
                _inventorySystem.TryEquip(user, legacyParent, legacyClothing, component.Slot, triggerHandContact: true);
            return;
        }
        // </Onyx-Modsuit>

        if (component.Container == null || component.ClothingUids.Count == 0)
            return;

        var parent = Transform(target).ParentUid;
        var allStored = component.ClothingUids.All(pair => component.Container.Contains(pair.Key));

        if (allStored)
        {
            foreach (var (part, slot) in component.ClothingUids)
                _inventorySystem.TryEquip(user, parent, part, slot, triggerHandContact: true);
            return;
        }

        foreach (var (part, slot) in component.ClothingUids)
        {
            if (!component.Container.Contains(part))
                _inventorySystem.TryUnequip(user, parent, slot, force: true);
        }
    }

    private void OnGetActions(EntityUid uid, ToggleableClothingComponent component, GetItemActionsEvent args)
    {
        if ((component.ClothingUid != null || component.ClothingUids.Count > 0)
            && component.ActionEntity != null
            && (args.SlotFlags & component.RequiredFlags) == component.RequiredFlags)
        {
            args.AddAction(component.ActionEntity.Value);
        }
    }

    private void OnInit(EntityUid uid, ToggleableClothingComponent component, ComponentInit args)
    {
        if (_containerSystem.TryGetContainer(uid, component.ContainerId, out var existing))
        {
            component.Container = existing;
            return;
        }

        component.Container = component.ClothingPrototypes.Count > 0
            ? _containerSystem.EnsureContainer<Container>(uid, component.ContainerId)
            : _containerSystem.EnsureContainer<ContainerSlot>(uid, component.ContainerId);
    }

    private void OnAttachedInit(EntityUid uid, AttachedClothingComponent component, ComponentInit args)
    {
        component.ReplacedClothing = _containerSystem.EnsureContainer<ContainerSlot>(uid,
            component.ReplacedClothingContainerIdField);
    }

    // <Onyx-Modsuit>
    public ToggleableClothingAttachedStatus GetAttachedToggleStatus(EntityUid user, EntityUid toggleable, bool unequipping, ToggleableClothingComponent? component = null)
    {
        if (!Resolve(toggleable, ref component) || component.Container == null || component.ClothingUids.Count == 0)
            return ToggleableClothingAttachedStatus.NoneToggled;

        var toggled = component.ClothingUids.Count(pair => !component.Container.Contains(pair.Key) && _inventorySystem.TryGetSlotEntity(user, pair.Value, out var equipped) && equipped == pair.Key);
        return toggled == 0 ? ToggleableClothingAttachedStatus.NoneToggled : toggled < component.ClothingUids.Count ? ToggleableClothingAttachedStatus.PartlyToggled : ToggleableClothingAttachedStatus.AllToggled;
    }

    public List<EntityUid>? GetAttachedClothingsList(EntityUid toggleable, ToggleableClothingComponent? component = null)
    {
        if (!Resolve(toggleable, ref component) || component.ClothingUids.Count == 0)
            return null;
        return component.ClothingUids.Keys.ToList();
    }
    // </Onyx-Modsuit>

    /// <summary>
    ///     On map init, either spawn the appropriate entity into the suit slot, or if it already exists, perform some
    ///     sanity checks. Also updates the action icon to show the toggled-entity.
    /// </summary>
    private void OnMapInit(EntityUid uid, ToggleableClothingComponent component, MapInitEvent args)
    {
        if (component.Container!.Count != 0)
        {
            DebugTools.Assert(component.ClothingUids.Count != 0 || component.ClothingUid != null,
                "Unexpected entity present inside of a toggleable clothing container.");
            return;
        }

        if (component.ClothingUids.Count != 0 && component.ActionEntity != null)
        {
            foreach (var attached in component.ClothingUids.Keys)
                DebugTools.Assert(TryComp(attached, out AttachedClothingComponent? comp) && comp.AttachedUid == uid, "Toggleable clothing uid mismatch");
        }
        else if (component.ClothingUids.Count == 0 && component.ClothingUid != null && component.ActionEntity != null)
        {
            DebugTools.Assert(TryComp(component.ClothingUid.Value, out AttachedClothingComponent? comp) && comp.AttachedUid == uid,
                "Toggleable clothing uid mismatch");
        }
        else
        {
            var xform = Transform(uid);
            if (component.ClothingPrototype is { } clothingPrototype &&
                !string.IsNullOrEmpty(component.Slot) &&
                !component.ClothingPrototypes.ContainsKey(component.Slot))
                component.ClothingPrototypes[component.Slot] = clothingPrototype;

            foreach (var prototype in component.ClothingPrototypes)
            {
                var spawned = Spawn(prototype.Value, xform.Coordinates);
                var attachedClothing = EnsureComp<AttachedClothingComponent>(spawned);
                attachedClothing.AttachedUid = uid;
                component.ClothingUids[spawned] = prototype.Key;
                component.ClothingUid ??= spawned;
                Dirty(spawned, attachedClothing);
                _containerSystem.Insert(spawned, component.Container, containerXform: xform);
            }
            Dirty(uid, component);
        }

        if (_actionContainer.EnsureAction(uid, ref component.ActionEntity, out var action, component.Action))
            _actionsSystem.SetEntityIcon((component.ActionEntity.Value, action), component.ClothingUid);
    }
}

public enum ToggleableClothingAttachedStatus : byte
{
    NoneToggled,
    PartlyToggled,
    AllToggled
}

public sealed partial class ToggleClothingEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class ToggleClothingDoAfterEvent : SimpleDoAfterEvent
{
}
