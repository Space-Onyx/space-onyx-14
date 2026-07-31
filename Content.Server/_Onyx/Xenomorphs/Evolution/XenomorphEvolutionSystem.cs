using System.Linq;
using Content.Server.Actions;
using Content.Server.Administration.Logs;
using Content.Server.DoAfter;
using Content.Server.Jittering;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared._Onyx.Xenomorphs.RadialSelector;
using Content.Shared._Onyx.Xenomorphs;
using Content.Shared._Onyx.Xenomorphs.Actions;
using Content.Shared._Onyx.Xenomorphs.Xenomorph;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Xenomorphs.Evolution;

public sealed partial class XenomorphEvolutionSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;

    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private JitteringSystem _jitter = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PlasmaCostActionSystem _plasmaCost = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenomorphEvolutionComponent, MapInitEvent>(OnXenomorphEvolutionMapInit);
        SubscribeLocalEvent<XenomorphEvolutionComponent, ComponentShutdown>(OnXenomorphEvolutionShutdown);
        SubscribeLocalEvent<XenomorphEvolutionComponent, EvolutionsActionEvent>(OnEvolutionsAction);
        SubscribeLocalEvent<XenomorphEvolutionComponent, RadialSelectorSelectedMessage>(OnEvolutionRecieved);
        SubscribeLocalEvent<XenomorphEvolutionComponent, XenomorphEvolutionDoAfterEvent>(OnXenomorphEvolutionDoAfter);
    }

    private void OnXenomorphEvolutionMapInit(EntityUid uid, XenomorphEvolutionComponent component, MapInitEvent args) =>
        _actions.AddAction(uid, ref component.EvolutionAction, component.EvolutionActionId);

    private void OnXenomorphEvolutionShutdown(EntityUid uid, XenomorphEvolutionComponent component, ComponentShutdown args) =>
        _actions.RemoveAction(uid, component.EvolutionAction);

    private void OnEvolutionsAction(EntityUid uid, XenomorphEvolutionComponent component, ref EvolutionsActionEvent args)
    {
        if (args.Handled)
            return;

        if (component.EvolvesTo.Count == 1)
        {
            if (component.Points < component.Max)
            {
                _popup.PopupEntity(Loc.GetString("xenomorphs-evolution-not-enough-points", ("seconds", (component.Max - component.Points) / component.PointsPerSecond)), uid, uid);
                return;
            }

            TryComp<PlasmaCostActionComponent>(args.Action, out var plasmaCost);
            args.Handled = Evolve(uid, component.EvolvesTo.First().Prototype, component.EvolutionDelay,
                uid, plasmaCost?.PlasmaCost ?? FixedPoint2.Zero);
            return;
        }

        _ui.TryToggleUi(uid, RadialSelectorUiKey.Key, uid);
        _ui.SetUiState(uid, RadialSelectorUiKey.Key, new TrackedRadialSelectorState(component.EvolvesTo));

        args.Handled = true;
    }

    private void OnEvolutionRecieved(EntityUid uid, XenomorphEvolutionComponent component, RadialSelectorSelectedMessage args)
    {
        if (component.Points < component.Max)
        {
            _popup.PopupEntity(Loc.GetString("xenomorphs-evolution-not-enough-points", ("seconds", (component.Max - component.Points) / component.PointsPerSecond)), uid, uid);
            return;
        }

        PlasmaCostActionComponent? plasmaCost = null;
        if (component.EvolutionAction is { } action)
            TryComp(action, out plasmaCost);
        if (Evolve(uid, args.SelectedItem, component.EvolutionDelay, uid,
                plasmaCost?.PlasmaCost ?? FixedPoint2.Zero))
            return;

        var actor = args.Actor;
        _ui.CloseUi(uid, RadialSelectorUiKey.Key, actor);
    }

    private void OnXenomorphEvolutionDoAfter(EntityUid uid, XenomorphEvolutionComponent component, ref XenomorphEvolutionDoAfterEvent args)
    {
        var plasmaPayer = GetEntity(args.PlasmaPayer);
        if (args.Handled || args.Cancelled || !_mind.TryGetMind(uid, out var mindUid, out var mind) ||
            !_plasmaCost.HasEnoughPlasma(plasmaPayer, args.PlasmaCost))
            return;

        var ev = new BeforeXenomorphEvolutionEvent(args.Caste, args.CheckNeedCasteDeath);
        RaiseLocalEvent(uid, ev);

        if (ev.Cancelled)
            return;

        args.Handled = true;
        _plasmaCost.DeductPlasma(plasmaPayer, args.PlasmaCost);

        var coordinates = _transform.GetMoverCoordinates(uid);
        var newXeno = Spawn(args.Choice, coordinates);

        _mind.TransferTo(mindUid, newXeno, mind:mind);
        _mind.UnVisit(mindUid, mind);

        var dropHandItemsEvent = new DropHandItemsEvent();
        RaiseLocalEvent(uid, ref dropHandItemsEvent);
        RaiseLocalEvent(uid, new AfterXenomorphEvolutionEvent(newXeno, mindUid, args.Caste));

        _adminLog.Add(LogType.Mind, $"{ToPrettyString(uid)} evolved into {ToPrettyString(newXeno)}");

        Del(uid);

        _popup.PopupEntity(Loc.GetString("xenomorphs-evolution-end"), newXeno, newXeno);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;

        var query = EntityQueryEnumerator<XenomorphEvolutionComponent>();
        while (query.MoveNext(out var uid, out var alienEvolution))
        {
            if (alienEvolution.Points == alienEvolution.Max || time < alienEvolution.NextPointsAt || _container.IsEntityInContainer(uid))
                continue;

            alienEvolution.NextPointsAt = time + TimeSpan.FromSeconds(1);
            alienEvolution.Points = FixedPoint2.Min(alienEvolution.Max, alienEvolution.Points + alienEvolution.PointsPerSecond);

            if (alienEvolution.Points != alienEvolution.Max)
                continue;

            _popup.PopupEntity(Loc.GetString("xenomorphs-evolution-ready"), uid, uid, PopupType.Large);
        }
    }

    public bool Evolve(EntityUid uid, string? evolveTo, TimeSpan evolutionDelay, EntityUid plasmaPayer,
        FixedPoint2 plasmaCost, bool checkNeedCasteDeath = true)
    {
        if (evolveTo == null
            || !_protoManager.TryIndex(evolveTo, out EntityPrototype? xenomorphPrototype)
            || !xenomorphPrototype.TryGetComponent<XenomorphComponent>(out var xenomorph, _componentFactory)
            || !_plasmaCost.HasEnoughPlasma(plasmaPayer, plasmaCost))
            return false;

        var ev = new BeforeXenomorphEvolutionEvent(xenomorph.Caste, checkNeedCasteDeath);
        RaiseLocalEvent(uid, ev);

        if (ev.Cancelled)
            return false;

        var doAfterEvent = new XenomorphEvolutionDoAfterEvent(evolveTo, xenomorph.Caste, GetNetEntity(plasmaPayer),
            plasmaCost, checkNeedCasteDeath);
        var doAfter = new DoAfterArgs(EntityManager, uid, evolutionDelay, doAfterEvent, uid);

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _jitter.DoJitter(uid, evolutionDelay, true, 80, 8, true);

        var popupOthers = Loc.GetString("xenomorphs-evolution-start-others", ("uid", uid));
        _popup.PopupEntity(popupOthers, uid, Filter.PvsExcept(uid), true, PopupType.Medium);

        var popupSelf = Loc.GetString("xenomorphs-evolution-start-self");
        _popup.PopupEntity(popupSelf, uid, uid, PopupType.Medium);

        return true;
    }
}
