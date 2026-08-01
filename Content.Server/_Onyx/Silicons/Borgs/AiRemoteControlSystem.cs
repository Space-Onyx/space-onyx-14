using Content.Server.Silicons.Laws;
using Content.Shared._Onyx.Silicons.Borgs;
using Content.Shared._Onyx.Silicons.Borgs.Components;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Onyx.Silicons.Borgs;

public sealed partial class AiRemoteControlSystem : SharedAiRemoteControlSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SiliconLawSystem _laws = default!;
    [Dependency] private SharedStationAiSystem _stationAi = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AiRemoteControllerComponent, ReturnMindIntoAiEvent>(OnReturnMindIntoAi);
        SubscribeLocalEvent<AiRemoteControllerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AiRemoteControllerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AiRemoteControllerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<StationAiHeldComponent, AiRemoteControllerComponent.RemoteDeviceActionMessage>(OnUiAction);
        SubscribeLocalEvent<StationAiHeldComponent, ToggleRemoteDevicesScreenEvent>(OnToggleUi);
    }

    private void OnReturnMindIntoAi(Entity<AiRemoteControllerComponent> ent, ref ReturnMindIntoAiEvent args) =>
        ReturnMindIntoAi(ent);

    private void OnMapInit(Entity<AiRemoteControllerComponent> ent, ref MapInitEvent args) =>
        _actions.AddAction(ent.Owner, ref ent.Comp.BackToAiActionEntity, ent.Comp.BackToAiAction);

    private void OnShutdown(Entity<AiRemoteControllerComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.BackToAiActionEntity);
        if (TryComp(ent, out IntrinsicRadioTransmitterComponent? transmitter) && ent.Comp.PreviouslyTransmitterChannels != null)
            transmitter.Channels = [.. ent.Comp.PreviouslyTransmitterChannels];
        if (TryComp(ent, out ActiveRadioComponent? active) && ent.Comp.PreviouslyActiveRadioChannels != null)
            active.Channels = [.. ent.Comp.PreviouslyActiveRadioChannels];
        ReturnMindIntoAi(ent);
    }

    private void OnGetVerbs(Entity<AiRemoteControllerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!HasComp<StationAiHeldComponent>(args.User))
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("ai-remote-control"), Act = () => TakeControl(user, ent) });
    }

    public void TakeControl(EntityUid ai, Entity<AiRemoteControllerComponent> target)
    {
        if (!_mind.TryGetMind(ai, out var mindId, out _) ||
            !TryComp<StationAiHeldComponent>(ai, out _) ||
            !_stationAi.TryGetCore(ai, out var core))
            return;

        if (TryComp(target, out IntrinsicRadioTransmitterComponent? transmitter))
        {
            target.Comp.PreviouslyTransmitterChannels = [.. transmitter.Channels];
            if (TryComp(ai, out IntrinsicRadioTransmitterComponent? aiTransmitter))
                transmitter.Channels = [.. aiTransmitter.Channels];
        }
        if (TryComp(target, out ActiveRadioComponent? active))
        {
            target.Comp.PreviouslyActiveRadioChannels = [.. active.Channels];
            if (TryComp(ai, out ActiveRadioComponent? aiActive))
                active.Channels = [.. aiActive.Channels];
        }

        _mind.ControlMob(ai, target);
        target.Comp.AiHolder = ai;
        target.Comp.LinkedMind = mindId;
        _stationAi.SwitchRemoteEntityMode(core, false);
        if (HasComp<SiliconLawProviderComponent>(ai) && HasComp<SiliconLawProviderComponent>(target))
            _laws.SetLaws(_laws.GetLaws(ai).Laws, target);
    }

    private void OnToggleUi(Entity<StationAiHeldComponent> ent, ref ToggleRemoteDevicesScreenEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;
        args.Handled = true;
        _ui.TryToggleUi(ent.Owner, RemoteDeviceUiKey.Key, actor.PlayerSession);
        var devices = new List<RemoteDevicesData>();
        var query = EntityQueryEnumerator<AiRemoteControllerComponent>();
        while (query.MoveNext(out var uid, out _))
            devices.Add(new RemoteDevicesData(Name(uid), GetNetEntity(uid)));
        _ui.SetUiState(ent.Owner, RemoteDeviceUiKey.Key, new RemoteDevicesBuiState(devices));
    }

    private void OnUiAction(Entity<StationAiHeldComponent> ent, ref AiRemoteControllerComponent.RemoteDeviceActionMessage msg)
    {
        var target = GetEntity(msg.RemoteAction.Target);
        if (!TryComp<AiRemoteControllerComponent>(target, out var controller))
            return;
        if (msg.RemoteAction.ActionType == RemoteDeviceActionType.TakeControl)
            TakeControl(ent.Owner, (target, controller));
        else if (_stationAi.TryGetCore(ent, out var core) && core.Comp?.RemoteEntity is { } remote)
            _transform.SetCoordinates(remote, Transform(target).Coordinates);
    }
}
