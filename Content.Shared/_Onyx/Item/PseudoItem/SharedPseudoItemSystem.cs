using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared._Onyx.Carrying;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Item.PseudoItem;

public abstract partial class SharedPseudoItemSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private CarryingSystem _carrying = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedItemSystem _items = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> PreventTag = "PreventLabel";

    private static readonly EntProtoId SleepAction = "ActionSleep";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PseudoItemComponent, GetVerbsEvent<InnateVerb>>(AddSelfInsertVerb);
        SubscribeLocalEvent<PseudoItemComponent, GetVerbsEvent<AlternativeVerb>>(AddOtherInsertVerb);
        SubscribeLocalEvent<PseudoItemComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<PseudoItemComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<PseudoItemComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<PseudoItemComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<PseudoItemComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<PseudoItemComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<PseudoItemComponent, PseudoItemInsertDoAfterEvent>(OnInsertDoAfter);
        SubscribeLocalEvent<PseudoItemComponent, TryingToSleepEvent>(OnTryingToSleep);
    }

    public bool CheckItemFits(Entity<PseudoItemComponent> item, Entity<StorageComponent> storage)
    {
        var fake = new ItemComponent { Owner = item };
        _items.SetSize(item, item.Comp.Size, fake);
        _items.SetShape(item, item.Comp.Shape, fake);
        _items.SetStoredOffset(item, item.Comp.StoredOffset, fake);
        return _storage.CanInsert(storage, item, out _, storage, fake, ignoreStacks: true);
    }

    public bool TryInsert(Entity<StorageComponent?> storage, Entity<PseudoItemComponent> item)
    {
        if (!Resolve(storage, ref storage.Comp) || item.Comp.Active || !CheckItemFits(item, (storage, storage.Comp)))
            return false;

        var itemComp = AddComp<ItemComponent>(item);
        _items.SetSize(item, item.Comp.Size, itemComp);
        _items.SetShape(item, item.Comp.Shape, itemComp);
        _items.SetStoredOffset(item, item.Comp.StoredOffset, itemComp);
        _items.VisualsChanged(item);
        _tags.TryAddTag(item, PreventTag);

        if (!_storage.Insert(storage, item, out _, storageComp: storage.Comp, stackAutomatically: false))
        {
            RemComp<ItemComponent>(item);
            return false;
        }

        item.Comp.Active = true;
        if (HasComp<AllowsSleepInsideComponent>(storage))
            _actions.AddAction(item, ref item.Comp.SleepAction, SleepAction, item);
        Dirty(item);
        return true;
    }

    private void AddSelfInsertVerb(Entity<PseudoItemComponent> ent, ref GetVerbsEvent<InnateVerb> args)
    {
        var target = args.Target;
        if (!args.CanInteract || !args.CanAccess || ent.Comp.Active ||
            !TryComp<StorageComponent>(target, out var storage) ||
            Transform(target).ParentUid == ent.Owner ||
            !CheckItemFits(ent, (target, storage)))
            return;

        args.Verbs.Add(new InnateVerb
        {
            Act = () => TryInsert((target, storage), ent),
            Text = Loc.GetString("action-name-insert-self"),
            Priority = 2,
        });
    }

    private void AddOtherInsertVerb(Entity<PseudoItemComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        if (!args.CanInteract || !args.CanAccess || ent.Comp.Active || args.Using is not { } usingEnt ||
            !TryComp<StorageComponent>(usingEnt, out var storage) ||
            !CheckItemFits(ent, (usingEnt, storage)) ||
            !_hands.TryGetActiveItem((user, args.Hands), out var held) || held != usingEnt)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartInsertDoAfter(user, ent, usingEnt),
            Text = Loc.GetString("action-name-insert-other", ("target", ent)),
            Priority = 2,
        });
    }

    private void StartInsertDoAfter(EntityUid user, EntityUid item, EntityUid storage)
    {
        var args = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5),
            new PseudoItemInsertDoAfterEvent(), item, item, storage)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("carry-started", ("carrier", user)), item, item);
    }

    private void OnInsertDoAfter(Entity<PseudoItemComponent> ent, ref PseudoItemInsertDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Used is not { } storage)
            return;

        args.Handled = TryInsert((storage, null), ent);
    }

    private void OnRemoved(Entity<PseudoItemComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (!ent.Comp.Active)
            return;

        RemComp<ItemComponent>(ent);
        ent.Comp.Active = false;
        _actions.RemoveAction(ent.Comp.SleepAction);
        ent.Comp.SleepAction = null;
        Dirty(ent);
    }

    private void OnPickupAttempt(Entity<PseudoItemComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (args.User == ent.Owner)
            return;

        if (TryComp<CarriableComponent>(ent, out var carriable) && _carrying.CanCarry(args.User, (ent, carriable)))
            _carrying.Carry(args.User, ent);
        else
            _transform.AttachToGridOrMap(ent);
        args.Cancel();
    }

    private void OnDropAttempt(Entity<PseudoItemComponent> ent, ref DropAttemptEvent args)
    {
        if (ent.Comp.Active)
            args.Cancel();
    }

    private void OnInsertAttempt(Entity<PseudoItemComponent> ent, ref ContainerGettingInsertedAttemptEvent args)
    {
        if (ent.Comp.Active)
            args.Cancel();
    }

    private void OnInteractionAttempt(Entity<PseudoItemComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.Active && args.Uid == args.Target)
            args.Cancelled = true;
    }

    private void OnAttackAttempt(Entity<PseudoItemComponent> ent, ref AttackAttemptEvent args)
    {
        if (ent.Comp.Active)
            args.Cancel();
    }

    private void OnTryingToSleep(Entity<PseudoItemComponent> ent, ref TryingToSleepEvent args)
    {
        var parent = Transform(ent).ParentUid;
        if (!HasComp<SleepingComponent>(ent) && parent.IsValid() && HasComp<AllowsSleepInsideComponent>(parent))
            _popup.PopupEntity(Loc.GetString("popup-sleep-in-bag", ("entity", ent)), ent);
    }
}
