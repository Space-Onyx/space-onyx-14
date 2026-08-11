using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.ItemOffer;

public abstract partial class SharedItemOfferSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<AlertPrototype> OfferAlert = "ItemOffer";

    public override void Initialize()
    {
        SubscribeLocalEvent<ItemOfferComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ItemOfferComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<ItemOfferComponent, AcceptItemOfferAlertEvent>(OnAcceptAlert);
        SubscribeLocalEvent<ItemOfferComponent, DidUnequipHandEvent>(OnItemUnequipped);
        _hands.OnHandSetActive += OnActiveHandChanged;

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OfferItem,
                InputCmdHandler.FromDelegate(ToggleOfferMode, handle: false, outsidePrediction: false))
            .Register<SharedItemOfferSystem>();
    }

    public override void Shutdown()
    {
        _hands.OnHandSetActive -= OnActiveHandChanged;
        CommandBinds.Unregister<SharedItemOfferSystem>();
        base.Shutdown();
    }

    public bool IsInOfferMode(EntityUid? uid)
    {
        return uid is { Valid: true } &&
               TryComp<ItemOfferComponent>(uid, out var component) &&
               component.IsInOfferMode;
    }

    private void ToggleOfferMode(ICommonSession? session)
    {
        if (!_timing.IsFirstTimePredicted ||
            session?.AttachedEntity is not { Valid: true } uid ||
            !_actionBlocker.CanInteract(uid, null) ||
            !TryComp<ItemOfferComponent>(uid, out var offer) ||
            !TryComp<HandsComponent>(uid, out var hands) ||
            hands.ActiveHandId is null)
            return;

        if (offer.IsInOfferMode || offer.IsInReceiveMode || offer.Target is not null)
        {
            Cancel(uid, offer, true);
            return;
        }

        var item = _hands.GetActiveItem(uid);
        if (item is null)
        {
            _popup.PopupEntity(Loc.GetString("item-offer-empty-hand"), uid, uid);
            return;
        }

        offer.IsInOfferMode = true;
        offer.Hand = hands.ActiveHandId;
        offer.Item = item;
        Dirty(uid, offer);
    }

    private void OnInteractUsing(Entity<ItemOfferComponent> receiver, ref InteractUsingEvent args)
    {
        if (!TryComp<ItemOfferComponent>(args.User, out var offerer) ||
            args.User == receiver.Owner ||
            receiver.Comp.IsInReceiveMode ||
            receiver.Comp.Target is not null ||
            !offerer.IsInOfferMode ||
            offerer.Item != args.Used)
            return;

        receiver.Comp.IsInReceiveMode = true;
        receiver.Comp.Target = args.User;
        Dirty(receiver);
        _alerts.ShowAlert(receiver.Owner, OfferAlert);

        offerer.IsInOfferMode = false;
        offerer.Target = receiver;
        Dirty(args.User, offerer);

        _popup.PopupPredicted(Loc.GetString("item-offer-start",
            ("item", Identity.Entity(args.Used, EntityManager)),
            ("target", Identity.Entity(receiver, EntityManager))), args.User, args.User);
        _popup.PopupClient(Loc.GetString("item-offer-start-target",
            ("user", Identity.Entity(args.User, EntityManager)),
            ("item", Identity.Entity(args.Used, EntityManager))), args.User, receiver);

        args.Handled = true;
    }

    private void OnAcceptAlert(Entity<ItemOfferComponent> receiver, ref AcceptItemOfferAlertEvent args)
    {
        if (args.Handled || args.AlertId != OfferAlert)
            return;

        args.Handled = true;
        Receive(receiver);
    }

    private void Receive(Entity<ItemOfferComponent> receiver)
    {
        if (!_timing.IsFirstTimePredicted ||
            receiver.Comp.Target is not { Valid: true } offererUid ||
            !TryComp<ItemOfferComponent>(offererUid, out var offerer) ||
            offerer.Item is not { Valid: true } item ||
            !TryComp<HandsComponent>(receiver, out var hands))
            return;

        if (!_transform.InRange(Transform(receiver).Coordinates, Transform(offererUid).Coordinates,
                receiver.Comp.MaxOfferDistance))
        {
            Cancel(receiver, receiver.Comp, true);
            return;
        }

        // Clear before pickup because hand events are raised synchronously during transfer.
        offerer.Item = null;
        if (!_hands.TryPickup(receiver, item, handsComp: hands))
        {
            offerer.Item = item;
            _popup.PopupClient(Loc.GetString("item-offer-full-hands"), receiver, receiver);
            return;
        }

        _popup.PopupClient(Loc.GetString("item-offer-complete",
            ("item", Identity.Entity(item, EntityManager)),
            ("target", Identity.Entity(receiver, EntityManager))), offererUid, offererUid);
        _popup.PopupPredicted(Loc.GetString("item-offer-complete-other",
            ("user", Identity.Entity(offererUid, EntityManager)),
            ("item", Identity.Entity(item, EntityManager)),
            ("target", Identity.Entity(receiver, EntityManager))), offererUid, receiver);

        Reset(receiver, receiver.Comp);
        Reset(offererUid, offerer);
    }

    private void OnMove(Entity<ItemOfferComponent> ent, ref MoveEvent args)
    {
        if (ent.Comp.Target is not { Valid: true } target ||
            _transform.InRange(args.NewPosition, Transform(target).Coordinates, ent.Comp.MaxOfferDistance))
            return;

        Cancel(ent, ent.Comp, true);
    }

    private void OnActiveHandChanged(Entity<HandsComponent>? ent)
    {
        if (ent is not { } hands ||
            !TryComp<ItemOfferComponent>(hands, out var offer) ||
            offer.Item is null ||
            offer.Hand == hands.Comp.ActiveHandId)
            return;

        Cancel(hands, offer, true);
    }

    private void OnItemUnequipped(Entity<ItemOfferComponent> ent, ref DidUnequipHandEvent args)
    {
        if (ent.Comp.Item == args.Unequipped)
            Cancel(ent, ent.Comp, true);
    }

    private void Cancel(EntityUid uid, ItemOfferComponent component, bool notify)
    {
        var targetUid = component.Target;
        TryComp<ItemOfferComponent>(targetUid, out var target);
        var offererUid = component.Item is not null ? uid : targetUid;
        var offerer = component.Item is not null ? component : target;

        if (notify && offererUid is { Valid: true } validOfferer && offerer?.Item is { Valid: true } item)
        {
            var receiver = validOfferer == uid ? targetUid : uid;
            if (receiver is { Valid: true } validReceiver)
            {
                _popup.PopupClient(Loc.GetString("item-offer-cancel",
                    ("item", Identity.Entity(item, EntityManager)),
                    ("target", Identity.Entity(validReceiver, EntityManager))), validOfferer, validOfferer);
                _popup.PopupEntity(Loc.GetString("item-offer-cancel-target",
                    ("user", Identity.Entity(validOfferer, EntityManager)),
                    ("item", Identity.Entity(item, EntityManager))), validOfferer, validReceiver);
            }
        }

        Reset(uid, component);
        if (targetUid is { Valid: true } validTarget && target is not null)
            Reset(validTarget, target);
    }

    private void Reset(EntityUid uid, ItemOfferComponent component)
    {
        component.IsInOfferMode = false;
        component.IsInReceiveMode = false;
        component.Hand = null;
        component.Item = null;
        component.Target = null;
        Dirty(uid, component);
        _alerts.ClearAlert(uid, OfferAlert);
    }
}
