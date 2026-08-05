using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Emp;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Content.Shared._Onyx.Surgery.Augments;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Surgery.Augments;

public sealed partial class AugmentItemPanelSystem : EntitySystem
{
    [Dependency] private AugmentSystem _augment = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AugmentItemPanelComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AugmentItemPanelComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<AugmentItemPanelComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<AugmentItemPanelComponent, AugmentItemPanelActionEvent>(OnToggle);
        SubscribeLocalEvent<AugmentItemPanelComponent, AugmentLostPowerEvent>(OnLostPower);
        SubscribeLocalEvent<AugmentItemPanelComponent, EmpPulseEvent>(OnEmp);
    }

    private void OnInit(Entity<AugmentItemPanelComponent> ent, ref ComponentInit args) => EnsureStoredItem(ent);

    private void OnInserted(Entity<AugmentItemPanelComponent> ent, ref OrganGotInsertedEvent args)
    {
        EnsureStoredItem(ent);
    }

    private void OnRemoved(Entity<AugmentItemPanelComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (ent.Comp.IsEquipped)
            Retract(ent, args.Target, false);
    }

    private void OnLostPower(Entity<AugmentItemPanelComponent> ent, ref AugmentLostPowerEvent args)
    {
        if (ent.Comp.RequiresPower && ent.Comp.IsEquipped)
            Retract(ent, args.Body, false);
    }

    private void OnEmp(Entity<AugmentItemPanelComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.IsEquipped && _augment.GetBody(ent.Owner) is { } body)
            Retract(ent, body, false);
    }

    private void OnToggle(Entity<AugmentItemPanelComponent> ent, ref AugmentItemPanelActionEvent args)
    {
        if (_augment.GetBody(ent.Owner) != args.Performer || !_augment.CanUse(ent.Owner, args.Performer))
            return;
        var cost = ent.Comp.IsEquipped ? ent.Comp.RetractPowerCost : ent.Comp.ExtendPowerCost;
        if (ent.Comp.RequiresPower && cost > 0f)
        {
            if (_augment.GetPowerSlot(args.Performer) == null)
            {
                _popup.PopupEntity(Loc.GetString("augments-no-power-cell-slot"), args.Performer, args.Performer,
                    PopupType.MediumCaution);
                return;
            }
            if (!_augment.TryUseCharge(args.Performer, cost, args.Performer))
                return;
        }
        if (ent.Comp.IsEquipped)
            Retract(ent, args.Performer, true);
        else
            Deploy(ent, args.Performer);
        args.Handled = true;
    }

    private void Deploy(Entity<AugmentItemPanelComponent> ent, EntityUid body)
    {
        if (!TryComp(body, out HandsComponent? hands) || !EnsureStoredItem(ent) || ent.Comp.SpawnedItem is not { } item)
            return;
        var hand = GetHand(ent.Owner, hands);
        if (hand == null)
        {
            _popup.PopupEntity(Loc.GetString("augment-item-panel-no-hand"), body, body, PopupType.SmallCaution);
            return;
        }
        if (_hands.GetHeldItem(body, hand) != null)
        {
            _popup.PopupEntity(Loc.GetString("augment-item-panel-hand-full"), body, body, PopupType.SmallCaution);
            return;
        }
        if (!_hands.TryForcePickup((body, hands), item, hand, checkActionBlocker: false))
        {
            _popup.PopupEntity(Loc.GetString("augment-item-panel-cannot-equip"), body, body, PopupType.SmallCaution);
            return;
        }
        EnsureComp<UnremoveableComponent>(item);
        ApplyAnimation(ent.Comp, item);
        if (ent.Comp.ExtendSound != null)
            _audio.PlayPvs(ent.Comp.ExtendSound, body);
        ent.Comp.IsEquipped = true;
        Dirty(ent);
        _toggle.TryActivate(ent.Owner, body);
        StartCooldown(ent.Owner);
        _popup.PopupEntity(Loc.GetString("augment-item-panel-deployed", ("item", item)), body, body);
    }

    private void Retract(Entity<AugmentItemPanelComponent> ent, EntityUid body, bool popup)
    {
        if (ent.Comp.SpawnedItem is not { } item)
        {
            ent.Comp.IsEquipped = false;
            Dirty(ent);
            return;
        }
        RemComp<UnremoveableComponent>(item);
        var storage = EnsureContainer(ent);
        if (!TerminatingOrDeleted(item) && !_containers.Insert(item, storage))
            return;
        if (ent.Comp.RetractSound != null)
            _audio.PlayPvs(ent.Comp.RetractSound, body);
        ent.Comp.IsEquipped = false;
        Dirty(ent);
        _toggle.TryDeactivate(ent.Owner, body);
        StartCooldown(ent.Owner);
        if (popup)
            _popup.PopupEntity(Loc.GetString("augment-item-panel-retracted", ("item", item)), body, body);
    }

    private string? GetHand(EntityUid augment, HandsComponent hands)
    {
        if (!TryComp(Transform(augment).ParentUid, out BodyPartComponent? part))
            return null;
        var location = part.Symmetry switch
        {
            BodyPartSymmetry.Left => HandLocation.Left,
            BodyPartSymmetry.Right => HandLocation.Right,
            _ => HandLocation.Middle,
        };
        foreach (var (id, hand) in hands.Hands)
            if (hand.Location == location)
                return id;
        return null;
    }

    private ContainerSlot EnsureContainer(Entity<AugmentItemPanelComponent> ent) =>
        _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.StorageContainerId);

    private bool EnsureStoredItem(Entity<AugmentItemPanelComponent> ent)
    {
        var storage = EnsureContainer(ent);
        if (ent.Comp.SpawnedItem is { } existing && !TerminatingOrDeleted(existing))
        {
            _augment.SetPowerProvider(existing, ent.Owner);
            return storage.ContainedEntity == existing || _containers.Insert(existing, storage);
        }
        var item = Spawn(ent.Comp.ItemPrototype, Transform(ent.Owner).Coordinates);
        if (!_containers.Insert(item, storage))
        {
            QueueDel(item);
            return false;
        }
        ent.Comp.SpawnedItem = item;
        _augment.SetPowerProvider(item, ent.Owner);
        Dirty(ent);
        return true;
    }

    private void ApplyAnimation(AugmentItemPanelComponent component, EntityUid item)
    {
        if (string.IsNullOrEmpty(component.ExtendHeldPrefix))
            return;
        _item.SetHeldPrefix(item, component.ExtendHeldPrefix);
        Timer.Spawn(component.ExtendHeldPrefixDuration, () =>
        {
            if (!TerminatingOrDeleted(item))
                _item.SetHeldPrefix(item, component.ExtendHeldPrefixAfter);
        });
    }

    private void StartCooldown(EntityUid augment)
    {
        if (TryComp(augment, out AugmentActionComponent? component) && component.ActionEntity is { } action)
            _actions.StartUseDelay(action);
    }
}
