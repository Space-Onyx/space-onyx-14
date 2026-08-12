using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Onyx.Carrying;

public sealed partial class CarryingSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private CarryingSlowdownSystem _slowdown = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CarriableComponent, GetVerbsEvent<AlternativeVerb>>(AddCarryVerb);
        SubscribeLocalEvent<CarriableComponent, CarryDoAfterEvent>(OnCarryDoAfter);
        SubscribeLocalEvent<BeingCarriedComponent, UpdateCanMoveEvent>(OnCannotMove);
        SubscribeLocalEvent<BeingCarriedComponent, StandAttemptEvent>(OnCannotStand);
        SubscribeLocalEvent<BeingCarriedComponent, GettingInteractedWithAttemptEvent>(OnInteractedWith);
        SubscribeLocalEvent<BeingCarriedComponent, EntityTerminatingEvent>(OnCarriedDeleted);
        SubscribeLocalEvent<CarryingComponent, MobStateChangedEvent>(OnCarrierStateChanged);
        SubscribeLocalEvent<CarryingComponent, EntParentChangedMessage>(OnCarrierParentChanged);
        SubscribeLocalEvent<CarryingComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
        SubscribeLocalEvent<CarryingComponent, BeforeThrowEvent>(OnThrow);
    }

    private void AddCarryVerb(Entity<CarriableComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.User == entity.Owner || !CanCarry(args.User, entity))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartCarry(user, entity),
            Text = Loc.GetString("carry-verb"),
            Priority = 2,
        });
    }

    private void StartCarry(EntityUid carrier, Entity<CarriableComponent> carried)
    {
        var args = new DoAfterArgs(EntityManager, carrier, TimeSpan.FromSeconds(3), new CarryDoAfterEvent(), carried, carried)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("carry-started", ("carrier", carrier)), carried, carried);
    }

    private void OnCarryDoAfter(Entity<CarriableComponent> entity, ref CarryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !CanCarry(args.Args.User, entity))
            return;

        Carry(args.Args.User, entity.Owner);
        args.Handled = true;
    }

    public void Carry(EntityUid carrier, EntityUid carried)
    {
        if (TryComp<PullableComponent>(carried, out var pullable))
            _pulling.TryStopPull(carried, pullable);

        _transform.AttachToGridOrMap(carrier, Transform(carrier));
        _transform.AttachToGridOrMap(carried, Transform(carried));
        _transform.SetParent(carried, Transform(carried), carrier, Transform(carrier));

        var carrying = EnsureComp<CarryingComponent>(carrier);
        carrying.Carried = carried;
        Dirty(carrier, carrying);
        var carriedComp = EnsureComp<BeingCarriedComponent>(carried);
        carriedComp.Carrier = carrier;
        Dirty(carried, carriedComp);
        EnsureComp<KnockedDownComponent>(carried);
        _slowdown.SetModifier((carrier, CompOrNull<CarryingSlowdownComponent>(carrier)), 0.75f);
        _actionBlocker.UpdateCanMove(carried);
        _virtualItem.TrySpawnVirtualItemInHand(carried, carrier);
        _virtualItem.TrySpawnVirtualItemInHand(carried, carrier);
    }

    public bool CanCarry(EntityUid carrier, Entity<CarriableComponent> carried)
    {
        return carrier != carried.Owner && !HasComp<CarryingComponent>(carrier) &&
               HasComp<MapGridComponent>(Transform(carrier).ParentUid) &&
               !HasComp<BeingCarriedComponent>(carrier) && !HasComp<BeingCarriedComponent>(carried) &&
               _hands.CountFreeHands(carrier) >= carried.Comp.FreeHandsRequired;
    }

    public void DropCarried(EntityUid carrier, EntityUid carried)
    {
        RemComp<BeingCarriedComponent>(carried);
        RemComp<CarryingComponent>(carrier);
        RemComp<CarryingSlowdownComponent>(carrier);
        _virtualItem.DeleteInHandsMatching(carrier, carried);
        _actionBlocker.UpdateCanMove(carried);
        _transform.AttachToGridOrMap(carried);
        _standing.Stand(carried);
    }

    private void OnCannotMove(Entity<BeingCarriedComponent> entity, ref UpdateCanMoveEvent args) => args.Cancel();
    private void OnCannotStand(Entity<BeingCarriedComponent> entity, ref StandAttemptEvent args) => args.Cancel();

    private void OnInteractedWith(Entity<BeingCarriedComponent> entity, ref GettingInteractedWithAttemptEvent args)
    {
        if (args.Uid != entity.Comp.Carrier)
            args.Cancelled = true;
    }

    private void OnCarriedDeleted(Entity<BeingCarriedComponent> entity, ref EntityTerminatingEvent args)
        => DropCarried(entity.Comp.Carrier, entity.Owner);

    private void OnCarrierStateChanged(Entity<CarryingComponent> entity, ref MobStateChangedEvent args)
        => DropCarried(entity.Owner, entity.Comp.Carried);

    private void OnCarrierParentChanged(Entity<CarryingComponent> entity, ref EntParentChangedMessage args)
    {
        if (Transform(entity).ParentUid != Transform(entity).GridUid)
            DropCarried(entity.Owner, entity.Comp.Carried);
    }

    private void OnVirtualItemDeleted(Entity<CarryingComponent> entity, ref VirtualItemDeletedEvent args)
    {
        if (args.BlockingEntity == entity.Comp.Carried)
            DropCarried(entity.Owner, entity.Comp.Carried);
    }

    private void OnThrow(Entity<CarryingComponent> entity, ref BeforeThrowEvent args)
    {
        if (entity.Owner != args.PlayerUid ||
            !TryComp<VirtualItemComponent>(args.ItemUid, out var virtualItem) ||
            virtualItem.BlockingEntity != entity.Comp.Carried)
            return;

        args.ItemUid = entity.Comp.Carried;
        if (TryComp<PhysicsComponent>(entity.Owner, out var carrierPhysics) &&
            TryComp<PhysicsComponent>(entity.Comp.Carried, out var carriedPhysics) &&
            carriedPhysics.Mass > 0f)
            args.ThrowSpeed = 5f * Math.Clamp(carrierPhysics.Mass / carriedPhysics.Mass, 0f, 2f);
        else
            args.ThrowSpeed = 5f;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BeingCarriedComponent, TransformComponent>();
        while (query.MoveNext(out var carried, out var component, out var transform))
        {
            if (TerminatingOrDeleted(component.Carrier) || transform.ParentUid != component.Carrier)
            {
                DropCarried(component.Carrier, carried);
                continue;
            }

            if (transform.LocalPosition != System.Numerics.Vector2.Zero)
                _transform.SetLocalPosition(carried, System.Numerics.Vector2.Zero);
        }
    }
}
