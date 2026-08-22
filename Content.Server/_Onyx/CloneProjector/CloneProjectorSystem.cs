using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Onyx.Carrying;
using Content.Shared._Onyx.CloneProjector;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Storage;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.CloneProjector;

public sealed partial class CloneProjectorSystem : SharedCloneProjectorSystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private CarryingSystem _carrying = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CloneProjectorComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<CloneProjectorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CloneProjectorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CloneProjectorComponent, GetItemActionsEvent>(OnEquipped);
        SubscribeLocalEvent<CloneProjectorComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<CloneProjectorComponent, CloneProjectorActivatedEvent>(OnProjectorActivated);
        SubscribeLocalEvent<WearingCloneProjectorComponent, MobStateChangedEvent>(OnWearerStateChanged);

        InitializeClone();
        _sawmill = Logger.GetSawmill("clone-projector");
    }

    private void OnInit(Entity<CloneProjectorComponent> projector, ref MapInitEvent args)
    {
        projector.Comp.CloneContainer = _container.EnsureContainer<Container>(projector, "CloneContainer");
    }

    private void OnExamined(Entity<CloneProjectorComponent> projector, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("clone-projector-examined-status", ("cloneStatus", IsCloneDeployed(projector.Comp))));

        if (!TryComp<DamageableComponent>(projector.Comp.CloneUid, out var damageable) ||
            !_thresholds.TryGetDeadThreshold(projector.Comp.CloneUid.Value, out var deathThreshold))
            return;

        var remainingHealth = deathThreshold - _damageable.GetTotalDamage((projector.Comp.CloneUid.Value, damageable));
        args.PushMarkup(Loc.GetString("clone-projector-examined-health", ("cloneHealth", remainingHealth / deathThreshold * 100)));
    }

    private void OnGetVerbs(Entity<CloneProjectorComponent> projector, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanComplexInteract || projector.Comp.CurrentHost != args.User ||
            !CanUseProjector(projector, args.User))
            return;

        var host = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => Regenerate(projector, host, false),
            Text = Loc.GetString("gemini-projector-regenerate-verb"),
            Message = Loc.GetString("gemini-projector-regenerate-verb-text"),
            Icon = new SpriteSpecifier.Rsi(new("Mobs/Silicon/station_ai.rsi"), "default"),
            Priority = 2,
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => Regenerate(projector, host, true),
            Text = Loc.GetString("gemini-projector-reboot-verb"),
            Message = Loc.GetString("gemini-projector-reboot-verb-text"),
            Icon = new SpriteSpecifier.Rsi(new("Mobs/Silicon/station_ai.rsi"), "default"),
            Priority = 2,
        });
    }

    private void Regenerate(Entity<CloneProjectorComponent> projector, EntityUid host, bool removeMind)
    {
        if (TryGenerateClone(projector, host, true, removeMind))
            DoCooldown(projector);
    }

    private void OnEquipped(Entity<CloneProjectorComponent> projector, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ref projector.Comp.ActionEntity, projector.Comp.Action);
        _popup.PopupEntity(Loc.GetString(projector.Comp.EquippedMessage), args.User, args.User);
        TryGenerateClone(projector, args.User);

        if (projector.Comp.DoStun)
            _stun.TryUpdateParalyzeDuration(args.User, projector.Comp.StunDuration);

        EnsureComp<WearingCloneProjectorComponent>(args.User).ConnectedProjector = projector;
    }

    private void OnUnequipped(Entity<CloneProjectorComponent> projector, ref GotUnequippedEvent args)
    {
        _actions.RemoveProvidedActions(args.EquipTarget, projector);
        TryInsertClone(projector);
        _popup.PopupEntity(Loc.GetString(projector.Comp.UnequippedMessage), args.EquipTarget, args.EquipTarget);

        if (projector.Comp.DoStun)
            _stun.TryUpdateParalyzeDuration(args.EquipTarget, projector.Comp.StunDuration);

        RemComp<WearingCloneProjectorComponent>(args.EquipTarget);
    }

    private void OnProjectorActivated(Entity<CloneProjectorComponent> projector, ref CloneProjectorActivatedEvent args)
    {
        if (args.Handled || !CanUseProjector(projector, args.Performer))
            return;

        var popup = Loc.GetString(projector.Comp.CloneGeneratedMessage,
            ("user", Identity.Name(args.Performer, EntityManager)));

        if (projector.Comp.CurrentHost == args.Performer && TryDeployClone(projector.Comp))
        {
            args.Handled = true;
            _popup.PopupEntity(popup, args.Performer, PopupType.Medium);
            return;
        }

        if (TryInsertClone(projector))
        {
            args.Handled = true;
            return;
        }

        if (!TryGenerateClone(projector, args.Performer))
            return;

        TryDeployClone(projector.Comp);
        _popup.PopupEntity(popup, args.Performer, PopupType.Medium);
        args.Handled = true;
    }

    private void OnWearerStateChanged(Entity<WearingCloneProjectorComponent> wearer, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || wearer.Comp.ConnectedProjector is not { } projector ||
            projector.Comp.CloneUid is not { } clone)
            return;

        CleanClone(clone, true);
        TryInsertClone(projector);
    }

    private bool TryGenerateClone(Entity<CloneProjectorComponent> projector, EntityUid performer, bool force = false,
        bool removeMind = false)
    {
        if (!TryComp<HumanoidProfileComponent>(performer, out var humanoid))
            return false;

        if (performer == projector.Comp.CurrentHost && !force)
            return false;

        if (!_prototypes.TryIndex(humanoid.Species, out var species))
            return false;

        var clone = Spawn(species.Prototype, Transform(performer).Coordinates);
        if (projector.Comp.CloneUid is { } oldClone)
        {
            _container.TryRemoveFromContainer(oldClone);
            CleanClone(oldClone, true);
            if (_mind.TryGetMind(oldClone, out var mindId, out _) && !removeMind)
                _mind.TransferTo(mindId, clone);
            Del(oldClone);
        }

        _container.Insert(clone, projector.Comp.CloneContainer);
        _visualBody.CopyAppearanceFrom(performer, clone);

        if (projector.Comp.AddedComponents != null)
            EntityManager.AddComponents(clone, projector.Comp.AddedComponents);
        if (projector.Comp.RemovedComponents != null)
        {
            foreach (var name in projector.Comp.RemovedComponents)
            {
                if (Factory.TryGetRegistration(name, out var registration))
                    RemComp(clone, registration.Type);
            }
        }

        projector.Comp.CurrentHost = performer;
        projector.Comp.CloneUid = clone;

        var hologram = EnsureComp<HolographicCloneComponent>(clone);
        hologram.HostProjector = projector;
        hologram.HostEntity = performer;

        _damageable.SetDamageModifierSetId(clone, projector.Comp.CloneDamageModifierSet);
        _meta.SetEntityName(clone, $"{Identity.Name(performer, EntityManager)} {Loc.GetString(projector.Comp.NameSuffix)}");

        if (!TryEquipItems(projector.Comp))
            _sawmill.Error($"Failed to equip items for holographic clone of {ToPrettyString(clone)}");

        var role = EnsureComp<GhostRoleComponent>(clone);
        role.RoleName = Loc.GetString(projector.Comp.GhostRoleName);
        role.RoleDescription = Loc.GetString(projector.Comp.GhostRoleDescription);
        role.RoleRules = Loc.GetString(projector.Comp.GhostRoleRules);

        if (projector.Comp.RequiredRole != null)
        {
            var requirement = EnsureComp<GhostRolePlaytimeRequirementComponent>(clone);
            requirement.Tracker = projector.Comp.RequiredRole.Value;
            requirement.Time = projector.Comp.TimeNeeded;
        }

        Dirty(projector);
        return true;
    }

    public bool TryInsertClone(Entity<CloneProjectorComponent> projector, bool doCooldown = false)
    {
        if (projector.Comp.CloneUid is not { } clone || !IsCloneDeployed(projector.Comp))
            return false;

        CleanClone(clone);
        _popup.PopupCoordinates(Loc.GetString(projector.Comp.CloneRetrievedMessage, ("target", Name(clone))),
            Transform(clone).Coordinates, PopupType.Medium);

        if (TerminatingOrDeleted(projector) || !_container.Insert(clone, projector.Comp.CloneContainer))
        {
            QueueDel(clone);
            return false;
        }

        if (doCooldown)
            DoCooldown(projector);

        Dirty(projector);
        return true;
    }

    private bool IsCloneDeployed(CloneProjectorComponent projector)
    {
        return projector.CloneUid is { } clone && !_container.IsEntityOrParentInContainer(clone);
    }

    private bool TryDeployClone(CloneProjectorComponent projector)
    {
        return projector.CloneUid is { } clone && !IsCloneDeployed(projector) && _container.TryRemoveFromContainer(clone);
    }

    private bool TryEquipItems(CloneProjectorComponent projector)
    {
        if (projector.CloneUid is not { } clone || projector.CurrentHost is not { } host)
            return false;

        var toSpawn = new Dictionary<EntProtoId, string>();
        var inventory = _inventory.GetSlotEnumerator(host);
        while (inventory.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } item)
                continue;

            if (_whitelist.IsWhitelistFail(projector.ClonedItemWhitelist, item) ||
                _whitelist.IsWhitelistPass(projector.ClonedItemBlacklist, item))
                continue;

            if (Prototype(item) is not { } proto)
                continue;

            toSpawn[proto] = slot.ID;
        }

        foreach (var item in toSpawn.Where(item => _inventory.SpawnItemInSlot(clone, item.Value, item.Key, true, true)))
        {
            if (!_inventory.TryGetSlotEntity(clone, item.Value, out var spawned))
                continue;

            EnsureComp<UnremoveableComponent>(spawned.Value);
            if (!TryComp<ItemSlotsComponent>(spawned, out var slots))
                continue;

            foreach (var slot in slots.Slots.Values)
            {
                if (slot.ContainerSlot != null)
                    _itemSlots.SetLock(spawned.Value, slot, true);
            }
        }

        if (TryComp<EncryptionKeyHolderComponent>(host, out var hostKeys) &&
            TryComp<EncryptionKeyHolderComponent>(clone, out var cloneKeys))
        {
            foreach (var key in hostKeys.KeyContainer.ContainedEntities)
            {
                if (TryPrototype(key, out var keyProto))
                    SpawnInContainerOrDrop(keyProto.ID, clone, cloneKeys.KeyContainer.ID);
            }
        }

        return true;
    }

    private void CleanClone(EntityUid clone, bool removePocketItems = false)
    {
        if (TerminatingOrDeleted(clone))
            return;

        _joints.RecursiveClearJoints(clone);
        foreach (var heldItem in _hands.EnumerateHeld(clone))
            _hands.TryDrop(clone, heldItem);

        if (TryComp<CarryingComponent>(clone, out var carrying))
            _carrying.DropCarried(clone, carrying.Carried);

        var inventory = _inventory.GetSlotEnumerator(clone, SlotFlags.WITHOUT_POCKET);
        while (inventory.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } item)
                continue;

            if (HasComp<UnremoveableComponent>(item) && TryComp<StorageComponent>(item, out var storage))
            {
                foreach (var storedItem in _container.EmptyContainer(storage.Container))
                    _physics.ApplyAngularImpulse(storedItem, ThrowingSystem.ThrowAngularImpulse);
            }

            if (_inventory.TryUnequip(clone, slot.ID, true))
                _physics.ApplyAngularImpulse(item, ThrowingSystem.ThrowAngularImpulse);
        }

        if (!removePocketItems)
            return;

        foreach (var item in _inventory.GetHandOrInventoryEntities(clone, SlotFlags.POCKET))
        {
            _container.TryRemoveFromContainer(item);
            _physics.ApplyAngularImpulse(item, ThrowingSystem.ThrowAngularImpulse);
        }
    }

    private void DoCooldown(Entity<CloneProjectorComponent> projector)
    {
        if (projector.Comp.ActionEntity is not { } action || !TryComp<ActionComponent>(action, out var actionComp))
            return;

        _actions.SetCooldown((action, actionComp), projector.Comp.DestroyedCooldown);
    }

    private bool CanUseProjector(Entity<CloneProjectorComponent> projector, EntityUid user)
    {
        return _whitelist.IsWhitelistFailOrNull(projector.Comp.UserBlacklist, user);
    }
}
