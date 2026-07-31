using Content.Server._Onyx.Xenomorphs.Evolution;
using Content.Server.Actions;
using Content.Server.Popups;
using Content.Shared._Onyx.Xenomorphs;
using Content.Shared._Onyx.Xenomorphs.Queen;
using Content.Shared._Onyx.Xenomorphs.Xenomorph;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;

namespace Content.Server._Onyx.Xenomorphs.Queen;

public sealed partial class XenomorphQueenSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private XenomorphEvolutionSystem _xenomorphEvolution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenomorphQueenComponent, PromotionActionEvent>(OnPromotionAction);
        SubscribeLocalEvent<XenomorphQueenComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<XenomorphQueenComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(EntityUid uid, XenomorphQueenComponent component, MapInitEvent args) =>
        _actions.AddAction(uid, ref component.PromotionAction, component.PromotionActionId);

    private void OnShutdown(EntityUid uid, XenomorphQueenComponent component, ComponentShutdown args) =>
        _actions.RemoveAction(uid, component.PromotionAction);

    private void OnPromotionAction(EntityUid uid, XenomorphQueenComponent component, PromotionActionEvent args)
    {
        // Goobstation start
        if (args.Target == EntityUid.Invalid || args.Target == args.Performer)
            return;

        // Additional validation in case the target is no longer valid
        if (!HasComp<XenomorphComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("xenomorphs-queen-promotion-invalid-target"), args.Performer);
            return;
        }

        if (!TryComp<XenomorphComponent>(args.Target, out var xenomorph))
            return;

        // Check if target is already a Praetorian or not in the whitelist
        if (xenomorph.Caste == "Praetorian" || !component.CasteWhitelist.Contains(xenomorph.Caste))
        {
            if (xenomorph.Caste == "Praetorian")
                _popup.PopupEntity(Loc.GetString("xenomorphs-queen-already-praetorian"), args.Performer);
            else
                _popup.PopupEntity(Loc.GetString("xenomorphs-queen-promotion-didnt-pass-whitelist"), args.Performer);
            return;
        }

        var target = args.Target;
        var targetName = Name(target);
        if (!_xenomorphEvolution.Evolve(target, component.PromoteTo, component.EvolutionDelay, uid,
                FixedPoint2.New(500), checkNeedCasteDeath: false))
            return;

        _popup.PopupEntity(Loc.GetString("xenomorphs-queen-promotion-success", ("target", targetName)), uid, uid);
        args.Handled = true;
    }
}
