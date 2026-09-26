using Content.Server.Power.Components;
using Content.Server.Popups;
using Content.Server.Emp;
using Content.Server._Onyx.Telecommunications.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared._Onyx.Telecommunications;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server._Onyx.Telecommunications;

public sealed partial class TelecommunicationsChainSystem : EntitySystem
{
    private const string ReceiverOutputPort = "TelecomReceiverOutput";
    private const string ProcessorInputPort = "TelecomProcessorInput";
    private const string ProcessorOutputPort = "TelecomProcessorOutput";
    private const string BusInputPort = "TelecomBusInput";
    private const string BusOutputPort = "TelecomBusOutput";
    private const string ServerInputPort = "TelecomServerInput";
    private const string ServerOutputPort = "TelecomServerOutput";
    private const string BroadcasterInputPort = "TelecomBroadcasterInput";
    private const string ServerMonitorOutputPort = "TelecomServerMonitorOutput";
    private const string ConsoleInputPort = "TelecomConsoleInput";
    private const string HubInputPort = "TelecomHubInput";
    private const string HubOutputPort = "TelecomHubOutput";
    private const string WeldingQuality = "Welding";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private PopupSystem _popup = default!;

    private float _maintenanceAccumulator;
    private readonly Dictionary<EntityUid, TelecomTransmissionContext> _transmissions = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TelecomSignalLogComponent, ExaminedEvent>(OnLogExamined);
        SubscribeLocalEvent<TelecomReceiverComponent, LinkAttemptEvent>(OnReceiverLinkAttempt);
        SubscribeLocalEvent<TelecomProcessorComponent, LinkAttemptEvent>(OnProcessorLinkAttempt);
        SubscribeLocalEvent<TelecomBusComponent, LinkAttemptEvent>(OnBusLinkAttempt);
        SubscribeLocalEvent<TelecomSignalLogComponent, LinkAttemptEvent>(OnServerLinkAttempt);
        SubscribeLocalEvent<TelecomBroadcasterComponent, LinkAttemptEvent>(OnBroadcasterLinkAttempt);
        SubscribeLocalEvent<TelecomTrafficConsoleComponent, LinkAttemptEvent>(OnConsoleLinkAttempt);
        SubscribeLocalEvent<TelecomHubComponent, LinkAttemptEvent>(OnHubLinkAttempt);
        SubscribeLocalEvent<TelecomRouterComponent, GotEmaggedEvent>(OnRouterEmagged);
        SubscribeLocalEvent<TelecomBroadcasterComponent, GotEmaggedEvent>(OnBroadcasterEmagged);
        SubscribeLocalEvent<TelecomBroadcasterComponent, DamageDealtEvent>(OnBroadcasterDamaged);
        SubscribeLocalEvent<TelecomBroadcasterComponent, EmpPulseEvent>(OnBroadcasterEmp);
        SubscribeLocalEvent<TelecomBroadcasterComponent, ExaminedEvent>(OnBroadcasterExamined);
        SubscribeLocalEvent<TelecomBroadcasterComponent, InteractUsingEvent>(OnBroadcasterMaintenanceInteract);
        SubscribeLocalEvent<TelecomBroadcasterComponent, TelecomMaintenanceFinishedEvent>(OnBroadcasterMaintenanceFinished);
        SubscribeLocalEvent<TelecomCalibrationComponent, InteractUsingEvent>(OnCalibrationInteract);
        SubscribeLocalEvent<TelecomCalibrationComponent, TelecomCalibrationFinishedEvent>(OnCalibrationFinished);
        SubscribeLocalEvent<TelecomCalibrationComponent, DamageDealtEvent>(OnCalibrationDamaged);
        SubscribeLocalEvent<TelecomCalibrationComponent, EmpPulseEvent>(OnCalibrationEmp);
        SubscribeLocalEvent<TelecomCalibrationComponent, ExaminedEvent>(OnCalibrationExamined);
        SubscribeLocalEvent<TelecomWearComponent, InteractUsingEvent>(OnWearInteract);
        SubscribeLocalEvent<TelecomWearComponent, TelecomMaintenanceFinishedEvent>(OnMaintenanceFinished);
        SubscribeLocalEvent<TelecomWearComponent, DamageDealtEvent>(OnWearDamaged);
        SubscribeLocalEvent<TelecomWearComponent, EmpPulseEvent>(OnWearEmp);
        SubscribeLocalEvent<TelecomWearComponent, ExaminedEvent>(OnWearExamined);
        SubscribeLocalEvent<TelecomSolarFlareEvent>(OnSolarFlare);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
    }

    public override void Update(float frameTime)
    {
        var nodeQuery = EntityQueryEnumerator<TelecomNodeComponent>();
        while (nodeQuery.MoveNext(out _, out var node))
        {
            if (node.CurrentLoad <= 0f && node.TelemetryLoad <= 0f)
                continue;

            node.CurrentLoad = Math.Max(
                0f,
                node.CurrentLoad - Math.Max(0f, node.LoadRecoveryPerSecond) * frameTime);

            node.TelemetryLoad = Math.Max(
                0f,
                node.TelemetryLoad - Math.Max(0f, node.TelemetryRecoveryPerSecond) * frameTime);
        }

        _maintenanceAccumulator += frameTime;
        if (_maintenanceAccumulator < 1f)
            return;

        var maintenanceDelta = _maintenanceAccumulator;
        _maintenanceAccumulator = 0f;

        var query = EntityQueryEnumerator<TelecomCalibrationComponent>();
        while (query.MoveNext(out var uid, out var calibration))
        {
            if (calibration.MaintenanceFree || !IsIntegratedProcessorActive(uid))
                continue;

            calibration.Calibration = Math.Clamp(
                calibration.Calibration - calibration.DecayPerHour / 3600f * maintenanceDelta,
                0f,
                100f);
        }

        var wearQuery = EntityQueryEnumerator<TelecomWearComponent>();
        while (wearQuery.MoveNext(out _, out var wear))
        {
            if (wear.MaintenanceFree)
                continue;

            wear.Condition = Math.Clamp(
                wear.Condition - wear.WearPerHour / 3600f * maintenanceDelta,
                0f,
                100f);
        }
    }

    public TelecomRouteResult RouteSignal(
        EntityUid radioSource,
        RadioChannelPrototype channel,
        EntityUid messageSource,
        string message)
    {
        var sourceLocation = GetNetworkLocation(radioSource);
        _transmissions[radioSource] = new TelecomTransmissionContext(sourceLocation.Map, message.Length);
        if (!TryFindServer(sourceLocation, channel.ID, out var server))
        {
            AddFallbackLog(sourceLocation, channel, messageSource, message,
                TelecomSignalStatus.NoRoute, message.Length);
            return new TelecomRouteResult(false, message);
        }

        var log = EnsureComp<TelecomSignalLogComponent>(server);
        var serverLocation = GetNetworkLocation(server);
        if (!TryRouteInterMap(_transmissions[radioSource], serverLocation.Map))
        {
            TryAddServerLog(server, log, channel, messageSource, message,
                TelecomSignalStatus.BroadcastLoss, message.Length);
            return new TelecomRouteResult(false, message);
        }

        var chainStatus = GetChainStatus(server, serverLocation, out var chain);
        if (chainStatus != TelecomSignalStatus.Routed)
        {
            TryAddServerLog(server, log, channel, messageSource, message,
                chainStatus, message.Length);
            return new TelecomRouteResult(false, message);
        }

        if (!log.RoutingEnabled)
        {
            TryAddServerLog(server, log, channel, messageSource, message,
                TelecomSignalStatus.RoutingDisabled, message.Length);
            return new TelecomRouteResult(false, message);
        }

        if (!TryComp<TelecomRouterComponent>(server, out var router))
        {
            AddLogEntry(log, channel, messageSource, message,
                TelecomSignalStatus.NoRoute, message.Length);
            return new TelecomRouteResult(false, message);
        }

        if (router.DisabledChannels.Contains(channel.ID))
        {
            TryAddServerLog(server, log, channel, messageSource, message,
                TelecomSignalStatus.ChannelDisabled, message.Length);
            return new TelecomRouteResult(false, message);
        }

        if (IsChannelFaulted(router, channel.ID))
        {
            TryAddServerLog(server, log, channel, messageSource, message,
                TelecomSignalStatus.ServerChannelError, message.Length);
            return new TelecomRouteResult(false, message);
        }

        var route = SelectRoute(chain);
        var receiverQuality = GetCalibrationQuality(route.Receiver);
        var processorQuality = GetCalibrationQuality(route.Processor);
        var busQuality = GetWearQuality(chain.Bus);
        var serverQuality = GetWearQuality(chain.Server);
        var broadcasterQuality = GetBroadcasterQuality(route.Broadcaster);
        var inputHubQuality = route.InputHub is { } inputHub ? GetWearQuality(inputHub) : 1f;
        var outputHubQuality = route.OutputHub is { } outputHub ? GetWearQuality(outputHub) : 1f;

        if (!chain.Standalone &&
            TryComp<TelecomReceiverComponent>(route.Receiver, out var receiver) &&
            _random.Prob(GetFailureChance(
                receiverQuality,
                receiver.LossExponent,
                receiver.LossChanceMultiplier)))
        {
            TryAddServerLog(server, log, channel, messageSource, message,
                TelecomSignalStatus.ReceptionLoss, message.Length);
            return new TelecomRouteResult(false, message);
        }

        if (!chain.Standalone &&
            TryComp<TelecomBusComponent>(chain.Bus, out var bus) &&
            _random.Prob(GetFailureChance(
                busQuality,
                bus.RouteLossExponent,
                bus.RouteLossChanceMultiplier)))
        {
            TryAddServerLog(server, log, channel, messageSource, message,
                TelecomSignalStatus.BusFault, message.Length);
            return new TelecomRouteResult(false, message);
        }

        if (!TryRouteHub(route.InputHub, inputHubQuality) ||
            !TryRouteHub(route.OutputHub, outputHubQuality))
        {
            TryAddServerLog(server, log, channel, messageSource, message,
                TelecomSignalStatus.BusFault, message.Length);
            return new TelecomRouteResult(false, message);
        }

        var metrics = ApplyTrafficLoad(
            chain,
            route,
            message.Length,
            router,
            receiverQuality,
            processorQuality,
            busQuality,
            serverQuality,
            broadcasterQuality,
            inputHubQuality,
            outputHubQuality,
            out var broadcasterSabotaged);
        var congestionDropChance = Math.Clamp(
            (metrics.MaxUtilization - router.CongestionDropThreshold) *
            router.CongestionDropChanceMultiplier,
            0f,
            Math.Max(0f, router.MaxCongestionDropChance));
        var totalDropChance = Math.Clamp(
            congestionDropChance,
            0f,
            Math.Max(0f, router.MaxTotalDropChance));
        var criticalOverload = metrics.MaxUtilization >= router.CriticalOverloadThreshold;
        if (criticalOverload)
        {
            totalDropChance = Math.Max(
                totalDropChance,
                Math.Clamp(router.CriticalOverloadDropChance, 0f, 1f));
        }

        if (_random.Prob(totalDropChance))
        {
            TryAddServerLog(server, log, channel, messageSource, message,
                TelecomSignalStatus.Congested, message.Length, metrics);
            return new TelecomRouteResult(false, message);
        }

        var garbleChance = Math.Clamp(
            GetProcessorGarbleChance(route.Processor, processorQuality) +
            Math.Max(0f, metrics.MaxUtilization - router.GarbleLoadThreshold) *
            router.GarbleLoadChanceMultiplier,
            0f,
            Math.Max(0f, router.MaxGarbleChance));
        if (criticalOverload)
        {
            garbleChance = Math.Max(
                garbleChance,
                Math.Clamp(router.CriticalOverloadGarbleChance, 0f, 1f));
        }

        var transmittedMessage = message;
        var garbled = router.Sabotaged ||
                      broadcasterSabotaged ||
                      _random.Prob(garbleChance);
        if (garbled)
            transmittedMessage = GarbleMessage(message, router);

        // The physical path crosses the shared bus again after processing.
        if (!chain.Standalone &&
            TryComp<TelecomBusComponent>(chain.Bus, out bus) &&
            _random.Prob(GetFailureChance(
                busQuality,
                bus.RouteLossExponent,
                bus.RouteLossChanceMultiplier)))
        {
            TryAddServerLog(server, log, channel, messageSource, transmittedMessage,
                TelecomSignalStatus.BusFault, transmittedMessage.Length, metrics);
            return new TelecomRouteResult(false, transmittedMessage);
        }

        if (TryStartChannelFault(router, channel.ID, serverQuality) ||
            _random.Prob(GetFailureChance(
                serverQuality,
                router.ChannelErrorExponent,
                router.ChannelErrorChanceMultiplier)))
        {
            TryAddServerLog(server, log, channel, messageSource, transmittedMessage,
                TelecomSignalStatus.ServerChannelError, transmittedMessage.Length, metrics);
            return new TelecomRouteResult(false, transmittedMessage);
        }

        if (!chain.Standalone &&
            TryComp<TelecomBroadcasterComponent>(route.Broadcaster, out var broadcaster) &&
            _random.Prob(GetFailureChance(
                broadcasterQuality,
                broadcaster.OutputLossExponent,
                broadcaster.OutputLossChanceMultiplier)))
        {
            TryAddServerLog(server, log, channel, messageSource, transmittedMessage,
                TelecomSignalStatus.BroadcastLoss, transmittedMessage.Length, metrics);
            return new TelecomRouteResult(false, transmittedMessage);
        }

        var routedStatus = garbled
            ? TelecomSignalStatus.Garbled
            : metrics.Quality < router.DegradedQualityThreshold ||
              metrics.MaxUtilization > router.DegradedLoadThreshold
                ? TelecomSignalStatus.Degraded
                : TelecomSignalStatus.Routed;
        TryAddServerLog(server, log, channel, messageSource, transmittedMessage,
            routedStatus, transmittedMessage.Length, metrics);
        return new TelecomRouteResult(true, transmittedMessage);
    }

    public HashSet<string> GetServerChannels(EntityUid server)
    {
        return TryComp<EncryptionKeyHolderComponent>(server, out var holder)
            ? new HashSet<string>(holder.Channels.Select(c => c.ToString()))
            : [];
    }

    public bool ServerHasChannel(EntityUid server, string channel)
    {
        return TryComp<EncryptionKeyHolderComponent>(server, out var holder) &&
               holder.Channels.Contains(channel);
    }

    public bool IsServerChannelEnabled(EntityUid server, string channel)
    {
        return TryComp<TelecomRouterComponent>(server, out var router) &&
               !router.DisabledChannels.Contains(channel) &&
               !IsChannelFaulted(router, channel);
    }

    public bool CanCommunicate(EntityUid source, TransformComponent receiverTransform)
    {
        return CanReach(GetNetworkLocation(source), GetNetworkLocation(receiverTransform));
    }

    public bool CanCommunicate(EntityUid source, EntityUid receiver)
    {
        return CanReach(GetNetworkLocation(source), GetNetworkLocation(receiver));
    }

    public bool IsServerLinkedToConsole(EntityUid server, EntityUid console)
    {
        return TryComp<DeviceLinkSourceComponent>(server, out var source) &&
               IsLinked(source, console, ServerMonitorOutputPort, ConsoleInputPort);
    }

    public void CompleteTransmission(EntityUid radioSource)
    {
        _transmissions.Remove(radioSource);
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (args.Cancelled ||
            args.Channel.LongRange ||
            TryComp<ActiveRadioComponent>(args.RadioReceiver, out var radio) && radio.GlobalReceive)
            return;

        if (!CanCommunicate(args.RadioSource, args.RadioReceiver))
        {
            args.Cancelled = true;
            return;
        }

        if (_transmissions.TryGetValue(args.RadioSource, out var transmission) &&
            !TryRouteInterMap(transmission, Transform(args.RadioReceiver).MapID))
        {
            args.Cancelled = true;
        }
    }

    private bool TryFindServer(TelecomNetworkLocation sourceLocation, string channel, out EntityUid result)
    {
        var query = EntityQueryEnumerator<TelecomServerComponent, TelecomRouterComponent,
            ApcPowerReceiverComponent, TransformComponent>();
        EntityUid? localIncompleteServer = null;
        EntityUid? remoteCompleteServer = null;
        EntityUid? remoteIncompleteServer = null;

        while (query.MoveNext(out var uid, out _, out _, out var power, out var transform))
        {
            var serverLocation = GetNetworkLocation(transform);
            if (!CanReach(sourceLocation, serverLocation) ||
                !power.Powered ||
                !ServerHasChannel(uid, channel))
                continue;

            var complete = GetChainStatus(uid, serverLocation, out _) == TelecomSignalStatus.Routed;
            if (sourceLocation.Map == serverLocation.Map)
            {
                if (complete)
                {
                    result = uid;
                    return true;
                }

                localIncompleteServer ??= uid;
                continue;
            }

            if (complete)
                remoteCompleteServer ??= uid;
            else
                remoteIncompleteServer ??= uid;
        }

        var fallbackServer = localIncompleteServer ?? remoteCompleteServer ?? remoteIncompleteServer;
        if (fallbackServer != null)
        {
            result = fallbackServer.Value;
            return true;
        }

        result = default;
        return false;
    }

    private List<EntityUid> GetLinkedPoweredProcessors(
        EntityUid busUid,
        TelecomNetworkLocation networkLocation)
    {
        var processors = new List<EntityUid>();

        if (!TryComp<DeviceLinkSinkComponent>(busUid, out var busSink) ||
            !TryComp<DeviceLinkSourceComponent>(busUid, out var busSource) ||
            !TryComp<ApcPowerReceiverComponent>(busUid, out var busPower) ||
            !busPower.Powered ||
            !IsSameNetworkLocation(GetNetworkLocation(busUid), networkLocation))
        {
            return processors;
        }

        foreach (var candidate in busSink.LinkedSources)
        {
            if (!HasComp<TelecomProcessorComponent>(candidate) ||
                !TryComp<DeviceLinkSourceComponent>(candidate, out var processorSource) ||
                !IsLinked(processorSource, busUid, ProcessorOutputPort, BusInputPort) ||
                !IsLinked(busSource, candidate, BusOutputPort, ProcessorInputPort) ||
                !TryComp<ApcPowerReceiverComponent>(candidate, out var processorPower) ||
                !processorPower.Powered ||
                !IsSameNetworkLocation(GetNetworkLocation(candidate), networkLocation))
            {
                continue;
            }

            processors.Add(candidate);
        }

        return processors;
    }

    private TelecomSignalStatus GetChainStatus(
        EntityUid server,
        TelecomNetworkLocation networkLocation,
        out TelecomChainSnapshot chain)
    {
        chain = default;

        if (TryComp<TelecomRouterComponent>(server, out var router) && router.Standalone)
        {
            chain = new TelecomChainSnapshot([], [], [], server, server, [], true);
            return TelecomSignalStatus.Routed;
        }

        if (!TryGetLinkedPoweredSource<TelecomBusComponent>(
                server,
                networkLocation,
                BusOutputPort,
                ServerInputPort,
                out var bus))
            return TelecomSignalStatus.NoBus;

        var receivers = GetLinkedPoweredSources<TelecomReceiverComponent>(
            bus,
            networkLocation,
            ReceiverOutputPort,
            BusInputPort);
        foreach (var hub in GetLinkedPoweredSources<TelecomHubComponent>(
                     bus,
                     networkLocation,
                     HubOutputPort,
                     BusInputPort))
        {
            receivers.AddRange(GetLinkedPoweredSources<TelecomReceiverComponent>(
                hub,
                networkLocation,
                ReceiverOutputPort,
                HubInputPort));
        }
        receivers = receivers.Distinct().ToList();
        if (receivers.Count == 0)
            return TelecomSignalStatus.NoReceiver;

        var processors = GetLinkedPoweredProcessors(bus, networkLocation);
        if (processors.Count == 0)
            return TelecomSignalStatus.NoProcessor;

        var broadcasters = GetLinkedPoweredBroadcasters(server, networkLocation);
        broadcasters = broadcasters.Distinct().ToList();
        if (broadcasters.Count == 0)
            return TelecomSignalStatus.NoBroadcaster;

        var hubs = new HashSet<EntityUid>();
        foreach (var hub in GetLinkedPoweredSources<TelecomHubComponent>(
                     bus,
                     networkLocation,
                     HubOutputPort,
                     BusInputPort))
            hubs.Add(hub);

        if (TryComp<DeviceLinkSourceComponent>(server, out var serverSource))
        {
            foreach (var hub in serverSource.LinkedPorts.Keys)
            {
                if (HasComp<TelecomHubComponent>(hub) &&
                    IsLinked(serverSource, hub, ServerOutputPort, HubInputPort) &&
                    TryComp<ApcPowerReceiverComponent>(hub, out var hubPower) &&
                    hubPower.Powered &&
                    IsSameNetworkLocation(GetNetworkLocation(hub), networkLocation))
                    hubs.Add(hub);
            }
        }

        chain = new TelecomChainSnapshot(
            receivers,
            processors,
            hubs.ToList(),
            bus,
            server,
            broadcasters);
        return TelecomSignalStatus.Routed;
    }

    public void RepairRouter(EntityUid server)
    {
        if (TryComp<TelecomRouterComponent>(server, out var router))
        {
            router.Sabotaged = false;
            router.ResetArmed = false;
        }

        if (!TryComp<DeviceLinkSourceComponent>(server, out var source))
            return;

        foreach (var broadcasterUid in source.LinkedPorts.Keys)
        {
            if (IsLinked(source, broadcasterUid, ServerOutputPort, BroadcasterInputPort) &&
                TryComp<TelecomBroadcasterComponent>(broadcasterUid, out var broadcaster))
            {
                broadcaster.Sabotaged = false;
            }

            if (!IsLinked(source, broadcasterUid, ServerOutputPort, HubInputPort) ||
                !TryComp<DeviceLinkSourceComponent>(broadcasterUid, out var hubSource))
                continue;

            foreach (var linkedBroadcaster in hubSource.LinkedPorts.Keys)
            {
                if (IsLinked(hubSource, linkedBroadcaster, HubOutputPort, BroadcasterInputPort) &&
                    TryComp<TelecomBroadcasterComponent>(linkedBroadcaster, out var linkedBroadcasterComponent))
                {
                    linkedBroadcasterComponent.Sabotaged = false;
                }
            }
        }
    }

    private void OnRouterEmagged(Entity<TelecomRouterComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || ent.Comp.Sabotaged)
            return;

        ent.Comp.Sabotaged = true;
        ent.Comp.ResetArmed = false;
        args.Handled = true;
        args.Repeatable = true;
    }

    private void OnBroadcasterEmagged(Entity<TelecomBroadcasterComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || ent.Comp.Sabotaged)
            return;

        ent.Comp.Sabotaged = true;
        args.Handled = true;
        args.Repeatable = true;
    }

    private void OnBroadcasterDamaged(Entity<TelecomBroadcasterComponent> ent, ref DamageDealtEvent args)
    {
        ent.Comp.Condition = Math.Max(
            0f,
            ent.Comp.Condition -
            args.Damage.GetTotal().Float() * ent.Comp.DamageLossMultiplier);
    }

    private void OnBroadcasterEmp(Entity<TelecomBroadcasterComponent> ent, ref EmpPulseEvent args)
    {
        var loss = Math.Clamp(args.EnergyConsumption / 1000f * 8f, 3f, 15f);
        ent.Comp.Condition = Math.Max(0f, ent.Comp.Condition - loss);
        args.Affected = true;
    }

    private void OnBroadcasterExamined(Entity<TelecomBroadcasterComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(
            "telecom-broadcaster-condition",
            ("condition", (int) MathF.Round(ent.Comp.Condition))));
    }

    private void OnBroadcasterMaintenanceInteract(
        Entity<TelecomBroadcasterComponent> ent,
        ref InteractUsingEvent args)
    {
        if (args.Handled ||
            ent.Comp.Condition >= 100f ||
            !_tools.HasQuality(args.Used, WeldingQuality))
        {
            return;
        }

        if (TryComp<WiresPanelComponent>(ent.Owner, out var panel) && !panel.Open)
        {
            _popup.PopupEntity(
                Loc.GetString("telecom-maintenance-panel-closed"),
                ent.Owner,
                args.User,
                PopupType.Medium);
            return;
        }

        args.Handled = _tools.UseTool(
            args.Used,
            args.User,
            ent.Owner,
            8f,
            WeldingQuality,
            new TelecomMaintenanceFinishedEvent(),
            1f);
    }

    private void OnBroadcasterMaintenanceFinished(
        Entity<TelecomBroadcasterComponent> ent,
        ref TelecomMaintenanceFinishedEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        ent.Comp.Condition = 100f;
        args.Handled = true;
        _popup.PopupEntity(
            Loc.GetString("telecom-maintenance-complete"),
            ent.Owner,
            args.User,
            PopupType.Medium);
    }

    private string GarbleMessage(string message, TelecomRouterComponent router)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var inMarkup = new bool[message.Length];
        for (var start = 0; start < message.Length; start++)
        {
            if (message[start] != '[')
                continue;

            var end = message.IndexOf(']', start + 1);
            if (end == -1)
                break;

            for (var j = start; j <= end; j++)
                inMarkup[j] = true;

            start = end;
        }

        var chars = message.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (inMarkup[i])
                continue;

            if (!char.IsWhiteSpace(chars[i]) &&
                _random.Prob(Math.Clamp(router.GarbleCharacterChance, 0f, 1f)))
            {
                chars[i] = _random.Prob(Math.Clamp(router.GarbleAlternateSymbolChance, 0f, 1f))
                    ? '~'
                    : '#';
            }
        }

        return new string(chars);
    }

    private bool TryGetLinkedPoweredSource<TComponent>(
        EntityUid sinkUid,
        TelecomNetworkLocation networkLocation,
        string sourcePort,
        string sinkPort,
        out EntityUid sourceUid)
        where TComponent : IComponent
    {
        if (!TryComp<DeviceLinkSinkComponent>(sinkUid, out var sink))
        {
            sourceUid = default;
            return false;
        }

        foreach (var candidate in sink.LinkedSources)
        {
            if (!HasComp<TComponent>(candidate) ||
                !TryComp<DeviceLinkSourceComponent>(candidate, out var source) ||
                !IsLinked(source, sinkUid, sourcePort, sinkPort) ||
                !TryComp<ApcPowerReceiverComponent>(candidate, out var power) ||
                !power.Powered ||
                !IsSameNetworkLocation(GetNetworkLocation(candidate), networkLocation))
            {
                continue;
            }

            sourceUid = candidate;
            return true;
        }

        sourceUid = default;
        return false;
    }

    private List<EntityUid> GetLinkedPoweredSources<TComponent>(
        EntityUid sinkUid,
        TelecomNetworkLocation networkLocation,
        string sourcePort,
        string sinkPort)
        where TComponent : IComponent
    {
        var result = new List<EntityUid>();
        if (!TryComp<DeviceLinkSinkComponent>(sinkUid, out var sink))
            return result;

        foreach (var candidate in sink.LinkedSources)
        {
            if (!HasComp<TComponent>(candidate) ||
                !TryComp<DeviceLinkSourceComponent>(candidate, out var source) ||
                !IsLinked(source, sinkUid, sourcePort, sinkPort) ||
                !TryComp<ApcPowerReceiverComponent>(candidate, out var power) ||
                !power.Powered ||
                !IsSameNetworkLocation(GetNetworkLocation(candidate), networkLocation))
            {
                continue;
            }

            result.Add(candidate);
        }

        return result;
    }

    private List<EntityUid> GetLinkedPoweredBroadcasters(
        EntityUid server,
        TelecomNetworkLocation networkLocation)
    {
        var result = new List<EntityUid>();
        if (!TryComp<DeviceLinkSourceComponent>(server, out var source))
            return result;

        foreach (var broadcasterUid in source.LinkedPorts.Keys)
        {
            if (!IsLinked(source, broadcasterUid, ServerOutputPort, BroadcasterInputPort) ||
                !TryComp<TelecomBroadcasterComponent>(broadcasterUid, out var broadcaster) ||
                !TryComp<ApcPowerReceiverComponent>(broadcasterUid, out var power) ||
                !power.Powered ||
                !IsSameNetworkLocation(GetNetworkLocation(broadcasterUid), networkLocation))
            {
                continue;
            }

            result.Add(broadcasterUid);
        }

        foreach (var hubUid in source.LinkedPorts.Keys)
        {
            if (!IsLinked(source, hubUid, ServerOutputPort, HubInputPort) ||
                !TryComp<TelecomHubComponent>(hubUid, out _) ||
                !TryComp<ApcPowerReceiverComponent>(hubUid, out var hubPower) ||
                !hubPower.Powered ||
                !IsSameNetworkLocation(GetNetworkLocation(hubUid), networkLocation) ||
                !TryComp<DeviceLinkSourceComponent>(hubUid, out var hubSource))
                continue;

            foreach (var broadcasterUid in hubSource.LinkedPorts.Keys)
            {
                if (IsLinked(hubSource, broadcasterUid, HubOutputPort, BroadcasterInputPort) &&
                    TryComp<TelecomBroadcasterComponent>(broadcasterUid, out _) &&
                    TryComp<ApcPowerReceiverComponent>(broadcasterUid, out var power) &&
                    power.Powered &&
                    IsSameNetworkLocation(GetNetworkLocation(broadcasterUid), networkLocation))
                {
                    result.Add(broadcasterUid);
                }
            }
        }

        return result;
    }

    private EntityUid? FindInputHub(EntityUid receiver, EntityUid bus, IReadOnlyCollection<EntityUid> activeHubs)
    {
        if (!TryComp<DeviceLinkSourceComponent>(receiver, out var receiverSource))
            return null;

        foreach (var hub in receiverSource.LinkedPorts.Keys)
        {
            if (activeHubs.Contains(hub) &&
                IsLinked(receiverSource, hub, ReceiverOutputPort, HubInputPort) &&
                TryComp<DeviceLinkSourceComponent>(hub, out var hubSource) &&
                IsLinked(hubSource, bus, HubOutputPort, BusInputPort))
                return hub;
        }

        return null;
    }

    private EntityUid? FindOutputHub(EntityUid server, EntityUid broadcaster, IReadOnlyCollection<EntityUid> activeHubs)
    {
        if (!TryComp<DeviceLinkSourceComponent>(server, out var serverSource))
            return null;

        foreach (var hub in serverSource.LinkedPorts.Keys)
        {
            if (activeHubs.Contains(hub) &&
                IsLinked(serverSource, hub, ServerOutputPort, HubInputPort) &&
                TryComp<DeviceLinkSourceComponent>(hub, out var hubSource) &&
                IsLinked(hubSource, broadcaster, HubOutputPort, BroadcasterInputPort))
                return hub;
        }

        return null;
    }

    private bool TryRouteHub(EntityUid? hub, float quality)
    {
        return hub == null ||
               !TryComp<TelecomHubComponent>(hub, out var hubComponent) ||
               !_random.Prob(GetFailureChance(
                   quality,
                   hubComponent.RouteLossExponent,
                   hubComponent.RouteLossChanceMultiplier));
    }

    private bool IsBusProcessorLinkedEitherWay(EntityUid busUid, EntityUid processorUid)
    {
        var busToProcessor =
            TryComp<DeviceLinkSourceComponent>(busUid, out var busSource) &&
            IsLinked(busSource, processorUid, BusOutputPort, ProcessorInputPort);

        var processorToBus =
            TryComp<DeviceLinkSourceComponent>(processorUid, out var processorSource) &&
            IsLinked(processorSource, busUid, ProcessorOutputPort, BusInputPort);

        return busToProcessor || processorToBus;
    }

    private int CountLinkedProcessorsForBus(EntityUid busUid)
    {
        var processors = new HashSet<EntityUid>();

        if (TryComp<DeviceLinkSourceComponent>(busUid, out var busSource))
        {
            foreach (var target in busSource.LinkedPorts.Keys)
            {
                if (HasComp<TelecomProcessorComponent>(target) &&
                    IsLinked(busSource, target, BusOutputPort, ProcessorInputPort))
                {
                    processors.Add(target);
                }
            }
        }

        if (TryComp<DeviceLinkSinkComponent>(busUid, out var busSink))
        {
            foreach (var sourceUid in busSink.LinkedSources)
            {
                if (HasComp<TelecomProcessorComponent>(sourceUid) &&
                    TryComp<DeviceLinkSourceComponent>(sourceUid, out var processorSource) &&
                    IsLinked(processorSource, busUid, ProcessorOutputPort, BusInputPort))
                {
                    processors.Add(sourceUid);
                }
            }
        }

        return processors.Count;
    }

    private int CountLinkedBusesForProcessor(EntityUid processorUid)
    {
        var buses = new HashSet<EntityUid>();

        if (TryComp<DeviceLinkSourceComponent>(processorUid, out var processorSource))
        {
            foreach (var target in processorSource.LinkedPorts.Keys)
            {
                if (HasComp<TelecomBusComponent>(target) &&
                    IsLinked(processorSource, target, ProcessorOutputPort, BusInputPort))
                {
                    buses.Add(target);
                }
            }
        }

        if (TryComp<DeviceLinkSinkComponent>(processorUid, out var processorSink))
        {
            foreach (var sourceUid in processorSink.LinkedSources)
            {
                if (HasComp<TelecomBusComponent>(sourceUid) &&
                    TryComp<DeviceLinkSourceComponent>(sourceUid, out var busSource) &&
                    IsLinked(busSource, processorUid, BusOutputPort, ProcessorInputPort))
                {
                    buses.Add(sourceUid);
                }
            }
        }

        return buses.Count;
    }

    private static bool IsLinked(
        DeviceLinkSourceComponent source,
        EntityUid sink,
        string sourcePort,
        string sinkPort)
    {
        foreach (var (linkedSink, links) in source.LinkedPorts)
        {
            if (linkedSink != sink)
                continue;

            foreach (var link in links)
            {
                if (link.Source == sourcePort && link.Sink == sinkPort)
                    return true;
            }
        }

        return false;
    }

    private void OnReceiverLinkAttempt(Entity<TelecomReceiverComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.Source != ent.Owner ||
            args.SourcePort != ReceiverOutputPort ||
            args.SinkPort is not (BusInputPort or HubInputPort))
            return;

        var validSink = args.SinkPort == BusInputPort && HasComp<TelecomBusComponent>(args.Sink) ||
                        args.SinkPort == HubInputPort && HasComp<TelecomHubComponent>(args.Sink);
        if (!validSink ||
            CountLinkedTargets<TelecomBusComponent>(ent.Owner, ReceiverOutputPort, BusInputPort) +
            CountLinkedTargets<TelecomHubComponent>(ent.Owner, ReceiverOutputPort, HubInputPort) >= ent.Comp.MaxBuses &&
            !IsAlreadyLinked(ent.Owner, args.Sink, ReceiverOutputPort, args.SinkPort))
        {
            args.Cancel();
        }
    }

    private void OnProcessorLinkAttempt(Entity<TelecomProcessorComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.Sink == ent.Owner &&
            args.SourcePort == BusOutputPort &&
            args.SinkPort == ProcessorInputPort)
        {
            if (!HasComp<TelecomBusComponent>(args.Source) ||
                CountLinkedBusesForProcessor(ent.Owner) >= ent.Comp.MaxBuses &&
                !IsBusProcessorLinkedEitherWay(args.Source, ent.Owner))
            {
                args.Cancel();
            }

            return;
        }

        if (args.Sink == ent.Owner &&
            args.SinkPort == ProcessorInputPort)
        {
            args.Cancel();
            return;
        }

        if (args.Source == ent.Owner &&
            args.SourcePort == ProcessorOutputPort &&
            args.SinkPort == BusInputPort)
        {
            if (!HasComp<TelecomBusComponent>(args.Sink) ||
                CountLinkedBusesForProcessor(ent.Owner) >= ent.Comp.MaxBuses &&
                !IsBusProcessorLinkedEitherWay(args.Sink, ent.Owner))
            {
                args.Cancel();
            }

            return;
        }

        if (args.Source == ent.Owner &&
            args.SourcePort == ProcessorOutputPort)
        {
            args.Cancel();
        }
    }

    private void OnBusLinkAttempt(Entity<TelecomBusComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.Sink == ent.Owner &&
            args.SourcePort == HubOutputPort &&
            args.SinkPort == BusInputPort)
        {
            if (!HasComp<TelecomHubComponent>(args.Source) ||
                CountLinkedSources<TelecomHubComponent>(ent.Owner, HubOutputPort, BusInputPort) +
                CountLinkedSources<TelecomReceiverComponent>(ent.Owner, ReceiverOutputPort, BusInputPort) >= ent.Comp.MaxReceivers &&
                !IsAlreadyLinked(args.Source, ent.Owner, HubOutputPort, BusInputPort))
            {
                args.Cancel();
            }

            return;
        }

        if (args.Sink == ent.Owner &&
            args.SourcePort == ReceiverOutputPort &&
            args.SinkPort == BusInputPort)
        {
            if (!HasComp<TelecomReceiverComponent>(args.Source) ||
                CountLinkedSources<TelecomReceiverComponent>(ent.Owner, ReceiverOutputPort, BusInputPort) >= ent.Comp.MaxReceivers &&
                !IsAlreadyLinked(args.Source, ent.Owner, ReceiverOutputPort, BusInputPort))
            {
                args.Cancel();
            }

            return;
        }

        if (args.Sink == ent.Owner &&
            args.SourcePort == ProcessorOutputPort &&
            args.SinkPort == BusInputPort)
        {
            if (!HasComp<TelecomProcessorComponent>(args.Source) ||
                CountLinkedProcessorsForBus(ent.Owner) >= ent.Comp.MaxProcessors &&
                !IsBusProcessorLinkedEitherWay(ent.Owner, args.Source))
            {
                args.Cancel();
            }

            return;
        }

        if (args.Sink == ent.Owner &&
            args.SinkPort == BusInputPort)
        {
            args.Cancel();
            return;
        }

        if (args.Source == ent.Owner &&
            args.SourcePort == BusOutputPort &&
            args.SinkPort == ProcessorInputPort)
        {
            if (!HasComp<TelecomProcessorComponent>(args.Sink) ||
                CountLinkedProcessorsForBus(ent.Owner) >= ent.Comp.MaxProcessors &&
                !IsBusProcessorLinkedEitherWay(ent.Owner, args.Sink))
            {
                args.Cancel();
            }

            return;
        }

        if (args.Source == ent.Owner &&
            args.SourcePort == BusOutputPort &&
            args.SinkPort == ServerInputPort)
        {
            if (!HasComp<TelecomServerComponent>(args.Sink) ||
                CountLinkedTargets<TelecomServerComponent>(ent.Owner, BusOutputPort, ServerInputPort) >= ent.Comp.MaxServers &&
                !IsAlreadyLinked(ent.Owner, args.Sink, BusOutputPort, ServerInputPort))
            {
                args.Cancel();
            }

            return;
        }

        if (args.Source == ent.Owner &&
            args.SourcePort == BusOutputPort)
        {
            args.Cancel();
        }
    }
    private void OnServerLinkAttempt(Entity<TelecomSignalLogComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.Sink == ent.Owner &&
            args.SourcePort == BusOutputPort &&
            args.SinkPort == ServerInputPort)
        {
            if (!HasComp<TelecomBusComponent>(args.Source) ||
                !TryComp<TelecomRouterComponent>(ent.Owner, out var router) ||
                CountLinkedSources<TelecomBusComponent>(ent.Owner, BusOutputPort, ServerInputPort) >= router.MaxBuses &&
                !IsAlreadyLinked(args.Source, ent.Owner, BusOutputPort, ServerInputPort))
            {
                args.Cancel();
            }

            return;
        }

        if (args.Source == ent.Owner &&
            args.SourcePort == ServerOutputPort &&
            args.SinkPort == BroadcasterInputPort &&
            !HasComp<TelecomBroadcasterComponent>(args.Sink))
        {
            args.Cancel();
            return;
        }

        if (args.Source == ent.Owner &&
            args.SourcePort == ServerMonitorOutputPort &&
            args.SinkPort == ConsoleInputPort &&
            !HasComp<TelecomTrafficConsoleComponent>(args.Sink))
        {
            args.Cancel();
        }
    }

    private void OnBroadcasterLinkAttempt(Entity<TelecomBroadcasterComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.Sink != ent.Owner ||
            args.SourcePort is not (ServerOutputPort or HubOutputPort) ||
            args.SinkPort != BroadcasterInputPort)
            return;

        if (!HasComp<TelecomServerComponent>(args.Source) && !HasComp<TelecomHubComponent>(args.Source) ||
            CountLinkedSources<TelecomServerComponent>(ent.Owner, ServerOutputPort, BroadcasterInputPort) +
            CountLinkedSources<TelecomHubComponent>(ent.Owner, HubOutputPort, BroadcasterInputPort) >= ent.Comp.MaxServers &&
            !IsAlreadyLinked(args.Source, ent.Owner, args.SourcePort, BroadcasterInputPort))
        {
            args.Cancel();
        }
    }

    private void OnHubLinkAttempt(Entity<TelecomHubComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.Sink == ent.Owner && args.SinkPort == HubInputPort)
        {
            var validSource = args.SourcePort == ReceiverOutputPort && HasComp<TelecomReceiverComponent>(args.Source) ||
                              args.SourcePort == ServerOutputPort && HasComp<TelecomServerComponent>(args.Source);
            if (!validSource ||
                CountLinkedSources<TelecomReceiverComponent>(ent.Owner, ReceiverOutputPort, HubInputPort) +
                CountLinkedSources<TelecomServerComponent>(ent.Owner, ServerOutputPort, HubInputPort) >= ent.Comp.MaxInputs &&
                !IsAlreadyLinked(args.Source, ent.Owner, args.SourcePort, HubInputPort))
            {
                args.Cancel();
            }

            return;
        }

        if (args.Source == ent.Owner && args.SourcePort == HubOutputPort)
        {
            var validSink = args.SinkPort == BusInputPort && HasComp<TelecomBusComponent>(args.Sink) ||
                            args.SinkPort == BroadcasterInputPort && HasComp<TelecomBroadcasterComponent>(args.Sink);
            if (!validSink ||
                CountLinkedTargets<TelecomBusComponent>(ent.Owner, HubOutputPort, BusInputPort) +
                CountLinkedTargets<TelecomBroadcasterComponent>(ent.Owner, HubOutputPort, BroadcasterInputPort) >= ent.Comp.MaxOutputs &&
                !IsAlreadyLinked(ent.Owner, args.Sink, HubOutputPort, args.SinkPort))
            {
                args.Cancel();
            }
        }
    }

    private void OnConsoleLinkAttempt(Entity<TelecomTrafficConsoleComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.Sink != ent.Owner ||
            args.SourcePort != ServerMonitorOutputPort ||
            args.SinkPort != ConsoleInputPort)
            return;

        if (!HasComp<TelecomServerComponent>(args.Source))
            args.Cancel();
    }

    private int CountLinkedTargets<TComponent>(EntityUid sourceUid, string sourcePort, string sinkPort)
        where TComponent : IComponent
    {
        if (!TryComp<DeviceLinkSourceComponent>(sourceUid, out var source))
            return 0;

        var count = 0;
        foreach (var target in source.LinkedPorts.Keys)
        {
            if (HasComp<TComponent>(target) &&
                IsLinked(source, target, sourcePort, sinkPort))
            {
                count++;
            }
        }

        return count;
    }

    private int CountLinkedSources<TComponent>(EntityUid sinkUid, string sourcePort, string sinkPort)
        where TComponent : IComponent
    {
        if (!TryComp<DeviceLinkSinkComponent>(sinkUid, out var sink))
            return 0;

        var count = 0;
        foreach (var source in sink.LinkedSources)
        {
            if (HasComp<TComponent>(source) &&
                TryComp<DeviceLinkSourceComponent>(source, out var sourceComp) &&
                IsLinked(sourceComp, sinkUid, sourcePort, sinkPort))
            {
                count++;
            }
        }

        return count;
    }

    private bool IsAlreadyLinked(EntityUid sourceUid, EntityUid sinkUid, string sourcePort, string sinkPort)
    {
        return TryComp<DeviceLinkSourceComponent>(sourceUid, out var source) &&
               IsLinked(source, sinkUid, sourcePort, sinkPort);
    }

    private void AddFallbackLog(
        TelecomNetworkLocation sourceLocation,
        RadioChannelPrototype channel,
        EntityUid messageSource,
        string message,
        TelecomSignalStatus status,
        int messageLength)
    {
        var query = EntityQueryEnumerator<TelecomServerComponent, TelecomSignalLogComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var log, out var transform))
        {
            if (!CanReach(sourceLocation, GetNetworkLocation(transform)))
                continue;

            AddLogEntry(log, channel, messageSource, message, status, messageLength);
            return;
        }
    }

    private void AddLogEntry(
        TelecomSignalLogComponent component,
        RadioChannelPrototype channel,
        EntityUid messageSource,
        string message,
        TelecomSignalStatus status,
        int messageLength,
        TelecomTrafficMetrics? metrics = null)
    {
        var traffic = metrics ?? TelecomTrafficMetrics.Empty;
        var storedMessage = status is TelecomSignalStatus.Routed
            or TelecomSignalStatus.Degraded
            or TelecomSignalStatus.Garbled
            ? message
            : string.Empty;
        component.Entries.Add(new TelecomSignalLogEntry(
            _timing.CurTime,
            channel.LocalizedName,
            storedMessage,
            status,
            messageLength,
            (int) MathF.Round(traffic.Quality * 100f),
            (int) MathF.Round(traffic.MaxUtilization * 100f),
            traffic.LatencyMilliseconds));
        component.Revision++;

        var overflow = component.Entries.Count - Math.Max(1, component.MaxEntries);
        if (overflow > 0)
            component.Entries.RemoveRange(0, overflow);
    }

    private bool TryAddServerLog(
        EntityUid server,
        TelecomSignalLogComponent log,
        RadioChannelPrototype channel,
        EntityUid messageSource,
        string message,
        TelecomSignalStatus status,
        int messageLength,
        TelecomTrafficMetrics? metrics = null)
    {
        if (TryComp<TelecomRouterComponent>(server, out var router) &&
            _random.Prob(GetFailureChance(
                GetWearQuality(server),
                router.LoggingFailureExponent,
                router.LoggingFailureChanceMultiplier)))
        {
            return false;
        }

        AddLogEntry(log, channel, messageSource, message, status, messageLength, metrics);
        return true;
    }

    private void OnLogExamined(Entity<TelecomSignalLogComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(
            "telecom-signal-log-count",
            ("count", ent.Comp.Entries.Count),
            ("capacity", ent.Comp.MaxEntries)));

        if (ent.Comp.Entries.Count == 0)
            return;

        var entry = ent.Comp.Entries[^1];
        args.PushMarkup(Loc.GetString(
            "telecom-signal-log-last",
            ("time", FormatTime(entry.Timestamp)),
            ("channel", FormattedMessage.EscapeText(entry.Channel)),
            ("message", string.IsNullOrEmpty(entry.Message)
                ? Loc.GetString("telecom-signal-log-lost")
                : FormattedMessage.EscapeText(entry.Message))));
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    public TelecomTrafficMetrics GetServerMetrics(EntityUid server)
    {
        var networkLocation = GetNetworkLocation(server);
        return TryComp<TelecomRouterComponent>(server, out var router) &&
               GetChainStatus(server, networkLocation, out var chain) == TelecomSignalStatus.Routed
            ? ReadTrafficMetrics(chain, router)
            : TelecomTrafficMetrics.Empty;
    }

    private TelecomNetworkLocation GetNetworkLocation(EntityUid uid)
    {
        return GetNetworkLocation(Transform(uid));
    }

    private static TelecomNetworkLocation GetNetworkLocation(TransformComponent transform)
    {
        return new TelecomNetworkLocation(transform.MapID, transform.GridUid);
    }

    private bool CanReach(TelecomNetworkLocation source, TelecomNetworkLocation destination)
    {
        if (source.Map == destination.Map)
            return true;

        return HasActiveTransmitter(source.Map) &&
               HasActiveTransmitter(destination.Map);
    }

    private bool TryRouteInterMap(TelecomTransmissionContext transmission, MapId destination)
    {
        if (transmission.SourceMap == destination)
            return true;

        if (transmission.Destinations.TryGetValue(destination, out var routed))
            return routed;

        if (!TryGetActiveTransmitter(transmission.SourceMap, out var sourceTransmitter) ||
            !TryGetActiveTransmitter(destination, out var destinationTransmitter))
        {
            transmission.Destinations[destination] = false;
            return false;
        }

        ApplyNodeLoad(sourceTransmitter, transmission.MessageLength);
        ApplyNodeLoad(destinationTransmitter, transmission.MessageLength);
        routed = !TransmitterDropsSignal(sourceTransmitter) &&
                 !TransmitterDropsSignal(destinationTransmitter);
        transmission.Destinations[destination] = routed;
        return routed;
    }

    private bool TryGetActiveTransmitter(MapId map, out EntityUid result)
    {
        result = default;
        var bestUtilization = float.MaxValue;
        var query = EntityQueryEnumerator<TelecomTransmitterComponent,
            ApcPowerReceiverComponent,
            TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var power, out var transform))
        {
            if (!power.Powered || transform.MapID != map)
                continue;

            var utilization = GetNodeUtilization(uid);
            if (utilization >= bestUtilization)
                continue;

            result = uid;
            bestUtilization = utilization;
        }

        return result.IsValid();
    }

    private bool TransmitterDropsSignal(EntityUid uid)
    {
        if (!TryComp<TelecomTransmitterComponent>(uid, out var transmitter))
            return true;

        var wearLoss = GetFailureChance(
            GetWearQuality(uid),
            transmitter.LossExponent,
            transmitter.LossChanceMultiplier);
        var congestionLoss = Math.Clamp(
            (GetNodeUtilization(uid) - transmitter.CongestionDropThreshold) *
            transmitter.CongestionDropChanceMultiplier,
            0f,
            Math.Max(0f, transmitter.MaxCongestionDropChance));
        return _random.Prob(Math.Clamp(wearLoss + congestionLoss, 0f, 1f));
    }

    private static bool IsSameNetworkLocation(
        TelecomNetworkLocation left,
        TelecomNetworkLocation right)
    {
        return left.Map == right.Map && left.Grid == right.Grid;
    }

    private bool HasActiveTransmitter(MapId map)
    {
        return TryGetActiveTransmitter(map, out _);
    }

    public List<TelecomHardwareInfo> GetServerHardwareInfo(EntityUid server)
    {
        var result = new List<TelecomHardwareInfo>();
        AddHardwareInfo(result, server, TelecomHardwareType.Server);
        var serverMap = Transform(server).MapID;
        var transmitterQuery = EntityQueryEnumerator<TelecomTransmitterComponent, TransformComponent>();
        while (transmitterQuery.MoveNext(out var transmitter, out _, out var transform))
        {
            if (transform.MapID == serverMap)
                AddHardwareInfo(result, transmitter, TelecomHardwareType.Transmitter);
        }

        if (TryComp<TelecomRouterComponent>(server, out var router) && router.Standalone)
            return result;

        var buses = GetLinkedSourcesRegardlessOfPower<TelecomBusComponent>(
            server,
            BusOutputPort,
            ServerInputPort);

        foreach (var bus in buses)
        {
            AddHardwareInfo(result, bus, TelecomHardwareType.Bus);

            foreach (var receiver in GetLinkedSourcesRegardlessOfPower<TelecomReceiverComponent>(
                         bus,
                         ReceiverOutputPort,
                         BusInputPort))
            {
                AddHardwareInfo(result, receiver, TelecomHardwareType.Receiver);
            }

            foreach (var hub in GetLinkedSourcesRegardlessOfPower<TelecomHubComponent>(
                         bus,
                         HubOutputPort,
                         BusInputPort))
            {
                AddHardwareInfo(result, hub, TelecomHardwareType.Hub);
                foreach (var receiver in GetLinkedSourcesRegardlessOfPower<TelecomReceiverComponent>(
                             hub,
                             ReceiverOutputPort,
                             HubInputPort))
                {
                    AddHardwareInfo(result, receiver, TelecomHardwareType.Receiver);
                }
            }

            var processors = new HashSet<EntityUid>();
            if (TryComp<DeviceLinkSinkComponent>(bus, out var busSink))
            {
                foreach (var candidate in busSink.LinkedSources)
                {
                    if (HasComp<TelecomProcessorComponent>(candidate) &&
                        IsBusProcessorLinkedEitherWay(bus, candidate))
                    {
                        processors.Add(candidate);
                    }
                }
            }

            if (TryComp<DeviceLinkSourceComponent>(bus, out var busSource))
            {
                foreach (var candidate in busSource.LinkedPorts.Keys)
                {
                    if (HasComp<TelecomProcessorComponent>(candidate) &&
                        IsBusProcessorLinkedEitherWay(bus, candidate))
                    {
                        processors.Add(candidate);
                    }
                }
            }

            foreach (var processor in processors)
                AddHardwareInfo(result, processor, TelecomHardwareType.Processor);
        }

        if (TryComp<DeviceLinkSourceComponent>(server, out var serverSource))
        {
            foreach (var candidate in serverSource.LinkedPorts.Keys)
            {
                if (HasComp<TelecomBroadcasterComponent>(candidate) &&
                    IsLinked(serverSource, candidate, ServerOutputPort, BroadcasterInputPort))
                {
                    AddHardwareInfo(result, candidate, TelecomHardwareType.Broadcaster);
                }

                if (!HasComp<TelecomHubComponent>(candidate) ||
                    !IsLinked(serverSource, candidate, ServerOutputPort, HubInputPort))
                    continue;

                AddHardwareInfo(result, candidate, TelecomHardwareType.Hub);
                if (!TryComp<DeviceLinkSourceComponent>(candidate, out var hubSource))
                    continue;

                foreach (var broadcaster in hubSource.LinkedPorts.Keys)
                {
                    if (HasComp<TelecomBroadcasterComponent>(broadcaster) &&
                        IsLinked(hubSource, broadcaster, HubOutputPort, BroadcasterInputPort))
                    {
                        AddHardwareInfo(result, broadcaster, TelecomHardwareType.Broadcaster);
                    }
                }
            }
        }

        result.Sort((left, right) =>
        {
            var typeComparison = left.Type.CompareTo(right.Type);
            return typeComparison != 0
                ? typeComparison
                : left.Index.CompareTo(right.Index);
        });
        return result;
    }

    private List<EntityUid> GetLinkedSourcesRegardlessOfPower<TComponent>(
        EntityUid sinkUid,
        string sourcePort,
        string sinkPort)
        where TComponent : IComponent
    {
        var result = new List<EntityUid>();
        if (!TryComp<DeviceLinkSinkComponent>(sinkUid, out var sink))
            return result;

        foreach (var candidate in sink.LinkedSources)
        {
            if (HasComp<TComponent>(candidate) &&
                TryComp<DeviceLinkSourceComponent>(candidate, out var source) &&
                IsLinked(source, sinkUid, sourcePort, sinkPort))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private void AddHardwareInfo(
        List<TelecomHardwareInfo> result,
        EntityUid uid,
        TelecomHardwareType type)
    {
        if (result.Any(entry => entry.Entity == GetNetEntity(uid)))
            return;

        var index = result.Count(entry => entry.Type == type) + 1;
        var powered = TryComp<ApcPowerReceiverComponent>(uid, out var power) && power.Powered;
        var integratedProcessorActive = IsIntegratedProcessorActive(uid);
        var calibration = integratedProcessorActive &&
                          TryComp<TelecomCalibrationComponent>(uid, out var calibrationComp)
            ? (int) MathF.Round(calibrationComp.Calibration)
            : -1;
        var wear = TryComp<TelecomWearComponent>(uid, out var wearComp)
            ? (int) MathF.Round(wearComp.Condition)
            : TryComp<TelecomBroadcasterComponent>(uid, out var broadcaster)
                ? (int) MathF.Round(broadcaster.Condition)
                : -1;
        var load = integratedProcessorActive &&
                   TryComp<TelecomNodeComponent>(uid, out var node)
            ? (int) MathF.Round(node.TelemetryLoad / Math.Max(0.1f, node.Bandwidth) * 100f)
            : -1;
        var bandwidth = TryComp<TelecomNodeComponent>(uid, out node)
            ? (int) MathF.Round(node.Bandwidth)
            : -1;

        result.Add(new TelecomHardwareInfo(
            GetNetEntity(uid),
            type,
            index,
            powered,
            calibration,
            wear,
            load,
            Name(uid),
            GetLinkCount(uid),
            bandwidth,
            GetLinkNames(uid)));
    }

    private int GetLinkCount(EntityUid uid)
    {
        var links = new HashSet<EntityUid>();
        if (TryComp<DeviceLinkSinkComponent>(uid, out var sink))
            links.UnionWith(sink.LinkedSources);
        if (TryComp<DeviceLinkSourceComponent>(uid, out var source))
            links.UnionWith(source.LinkedPorts.Keys);
        return links.Count;
    }

    private List<string> GetLinkNames(EntityUid uid)
    {
        var links = new HashSet<EntityUid>();
        if (TryComp<DeviceLinkSinkComponent>(uid, out var sink))
            links.UnionWith(sink.LinkedSources);
        if (TryComp<DeviceLinkSourceComponent>(uid, out var source))
            links.UnionWith(source.LinkedPorts.Keys);

        return links.Select(link => Name(link)).Order().ToList();
    }

    private TelecomTrafficMetrics ApplyTrafficLoad(
        TelecomChainSnapshot chain,
        TelecomSelectedRoute route,
        int messageLength,
        TelecomRouterComponent router,
        float receiverQuality,
        float processorQuality,
        float busQuality,
        float serverQuality,
        float broadcasterQuality,
        float inputHubQuality,
        float outputHubQuality,
        out bool broadcasterSabotaged)
    {
        ApplyNodeLoad(route.Processor, messageLength);
        if (route.InputHub is { } inputHub)
            ApplyNodeLoad(inputHub, messageLength);
        if (route.OutputHub is { } outputHub && outputHub != route.InputHub)
            ApplyNodeLoad(outputHub, messageLength);

        broadcasterSabotaged = !chain.Standalone &&
            TryComp<TelecomBroadcasterComponent>(route.Broadcaster, out var broadcasterComponent) &&
            broadcasterComponent.Sabotaged;
        var quality = chain.Standalone
            ? (processorQuality + serverQuality) / 2f
            : (receiverQuality +
               processorQuality +
               busQuality +
               serverQuality +
               broadcasterQuality +
               (route.InputHub == null ? 0f : inputHubQuality) +
               (route.OutputHub == null ? 0f : outputHubQuality)) /
              (5f + (route.InputHub == null ? 0f : 1f) + (route.OutputHub == null ? 0f : 1f));
        return BuildTrafficMetrics(
            quality,
            processorQuality,
            Math.Max(
                GetNodeUtilization(route.Processor),
                Math.Max(
                    route.InputHub is { } inputHubUid ? GetNodeUtilization(inputHubUid) : 0f,
                    route.OutputHub is { } outputHubUid ? GetNodeUtilization(outputHubUid) : 0f)),
            route.Processor,
            router);
    }

    private void ApplyNodeLoad(EntityUid uid, int messageLength)
    {
        if (!TryComp<TelecomNodeComponent>(uid, out var node))
            return;

        var packetCost = Math.Max(0f, node.BasePacketCost) +
            messageLength / Math.Max(1f, node.CharactersPerCostUnit);
        node.CurrentLoad += packetCost;
        node.TelemetryLoad = Math.Max(node.TelemetryLoad, node.CurrentLoad);
    }

    private TelecomTrafficMetrics ReadTrafficMetrics(
        TelecomChainSnapshot chain,
        TelecomRouterComponent router)
    {
        if (chain.Standalone)
            return BuildTrafficMetrics(
                (GetCalibrationQuality(chain.Server) + GetWearQuality(chain.Server)) / 2f,
                GetCalibrationQuality(chain.Server),
                GetNodeTelemetryUtilization(chain.Server),
                chain.Server,
                router);

        var receiverMetrics = ReadPoolMetrics(chain.Receivers);
        var processorMetrics = ReadPoolMetrics(chain.Processors);
        var broadcasterMetrics = ReadPoolMetrics(chain.Broadcasters);
        var hubMetrics = ReadPoolMetrics(chain.Hubs);
        var busMetrics = ReadPoolMetrics([chain.Bus]);
        var serverMetrics = ReadPoolMetrics([chain.Server]);

        var qualityTotal =
            receiverMetrics.Quality +
            processorMetrics.Quality +
            busMetrics.Quality +
            serverMetrics.Quality +
            broadcasterMetrics.Quality;
        var qualityDivisor = 5f;
        if (chain.Hubs.Count > 0)
        {
            qualityTotal += hubMetrics.Quality;
            qualityDivisor++;
        }

        var quality = qualityTotal / qualityDivisor;
        return BuildTrafficMetrics(
            quality,
            processorMetrics.Quality,
            Math.Max(processorMetrics.Utilization, hubMetrics.Utilization),
            chain.Processors[0],
            router);
    }

    private TelecomPoolMetrics ReadPoolMetrics(IEnumerable<EntityUid> nodes)
    {
        var qualityTotal = 0f;
        var qualityCount = 0;
        var totalBandwidth = 0f;
        var totalLoad = 0f;

        foreach (var uid in nodes)
        {
            qualityTotal += GetNodeQuality(uid);
            qualityCount++;

            if (TryComp<TelecomNodeComponent>(uid, out var node))
            {
                var bandwidth = Math.Max(0.1f, node.Bandwidth);
                totalBandwidth += bandwidth;
                totalLoad += node.TelemetryLoad;
            }
        }

        var quality = qualityCount == 0 ? 1f : qualityTotal / qualityCount;
        var utilization = totalBandwidth <= 0f ? 0f : totalLoad / totalBandwidth;
        return new TelecomPoolMetrics(quality, utilization);
    }

    private TelecomTrafficMetrics BuildTrafficMetrics(
        float quality,
        float processorQuality,
        float maxUtilization,
        EntityUid processor,
        TelecomRouterComponent router)
    {
        var calibrationLatencyMultiplier =
            TryComp<TelecomProcessorComponent>(processor, out var processorComp)
                ? processorComp.CalibrationLatencyMultiplier
                : 0f;
        var latency = (int) MathF.Round(
            router.BaseLatencyMilliseconds +
            (1f - processorQuality) * calibrationLatencyMultiplier +
            Math.Max(0f, maxUtilization - router.LoadLatencyThreshold) *
            router.LoadLatencyMultiplier);
        return new TelecomTrafficMetrics(quality, maxUtilization, latency);
    }

    private TelecomSelectedRoute SelectRoute(TelecomChainSnapshot chain)
    {
        if (chain.Standalone)
            return new TelecomSelectedRoute(chain.Server, chain.Server, chain.Server, null, null);

        var receiver = chain.Receivers[_random.Next(chain.Receivers.Count)];
        var broadcaster = chain.Broadcasters[_random.Next(chain.Broadcasters.Count)];
        return new TelecomSelectedRoute(
            receiver,
            GetLeastLoadedNode(chain.Processors),
            broadcaster,
            FindInputHub(receiver, chain.Bus, chain.Hubs),
            FindOutputHub(chain.Server, broadcaster, chain.Hubs));
    }

    private EntityUid GetLeastLoadedNode(IReadOnlyList<EntityUid> nodes)
    {
        var selected = nodes[0];
        var selectedUtilization = GetNodeUtilization(selected);

        for (var i = 1; i < nodes.Count; i++)
        {
            var utilization = GetNodeUtilization(nodes[i]);
            if (utilization >= selectedUtilization)
                continue;

            selected = nodes[i];
            selectedUtilization = utilization;
        }

        return selected;
    }

    private float GetNodeUtilization(EntityUid uid)
    {
        return TryComp<TelecomNodeComponent>(uid, out var node)
            ? node.CurrentLoad / Math.Max(0.1f, node.Bandwidth)
            : 0f;
    }

    private float GetNodeTelemetryUtilization(EntityUid uid)
    {
        return TryComp<TelecomNodeComponent>(uid, out var node)
            ? node.TelemetryLoad / Math.Max(0.1f, node.Bandwidth)
            : 0f;
    }

    private float GetNodeQuality(EntityUid uid)
    {
        if (HasComp<TelecomBroadcasterComponent>(uid))
            return GetBroadcasterQuality(uid);
        if (HasComp<TelecomWearComponent>(uid))
            return GetWearQuality(uid);
        return GetCalibrationQuality(uid);
    }

    private bool IsIntegratedProcessorActive(EntityUid uid)
    {
        return !HasComp<TelecomServerComponent>(uid) ||
               TryComp<TelecomRouterComponent>(uid, out var router) && router.Standalone;
    }

    private float GetCalibrationQuality(EntityUid uid)
    {
        return TryComp<TelecomCalibrationComponent>(uid, out var calibration)
            ? Math.Clamp(calibration.Calibration / 100f, 0f, 1f)
            : 1f;
    }

    private float GetWearQuality(EntityUid uid)
    {
        return TryComp<TelecomWearComponent>(uid, out var wear)
            ? Math.Clamp(wear.Condition / 100f, 0f, 1f)
            : 1f;
    }

    private float GetBroadcasterQuality(EntityUid uid)
    {
        return TryComp<TelecomBroadcasterComponent>(uid, out var broadcaster)
            ? Math.Clamp(broadcaster.Condition / 100f, 0f, 1f)
            : 1f;
    }

    private static float GetFailureChance(float quality, float exponent, float multiplier)
    {
        return Math.Clamp(
            MathF.Pow(1f - Math.Clamp(quality, 0f, 1f), Math.Max(0.1f, exponent)) *
            Math.Max(0f, multiplier),
            0f,
            1f);
    }

    private float GetProcessorGarbleChance(EntityUid processor, float quality)
    {
        if (!TryComp<TelecomProcessorComponent>(processor, out var processorComp))
            return 0f;

        return Math.Max(0f, processorComp.GarbleQualityThreshold - quality) *
               Math.Max(0f, processorComp.GarbleChanceMultiplier);
    }

    private bool IsChannelFaulted(TelecomRouterComponent router, string channel)
    {
        if (!router.FaultedChannels.TryGetValue(channel, out var until))
            return false;

        if (until > _timing.CurTime)
            return true;

        router.FaultedChannels.Remove(channel);
        return false;
    }

    private bool TryStartChannelFault(
        TelecomRouterComponent router,
        string channel,
        float serverQuality)
    {
        if (IsChannelFaulted(router, channel) ||
            !_random.Prob(GetFailureChance(
                serverQuality,
                router.ChannelOutageExponent,
                router.ChannelOutageChanceMultiplier)))
        {
            return false;
        }

        router.FaultedChannels[channel] =
            _timing.CurTime + TimeSpan.FromSeconds(Math.Max(1f, router.ChannelOutageDurationSeconds));
        return true;
    }

    private void OnCalibrationInteract(Entity<TelecomCalibrationComponent> ent, ref InteractUsingEvent args)
    {
        if (ent.Comp.MaintenanceFree || !IsIntegratedProcessorActive(ent.Owner))
            return;

        if (args.Handled || !_tools.HasQuality(args.Used, SharedToolSystem.PulseQuality))
            return;

        if (TryComp<WiresPanelComponent>(ent.Owner, out var panel) && !panel.Open)
        {
            _popup.PopupEntity(
                Loc.GetString("telecom-calibration-panel-closed"),
                ent.Owner,
                args.User,
                PopupType.Small);
            return;
        }

        args.Handled = _tools.UseTool(
            args.Used,
            args.User,
            ent.Owner,
            6f,
            SharedToolSystem.PulseQuality,
            new TelecomCalibrationFinishedEvent());
    }

    private void OnCalibrationFinished(
        Entity<TelecomCalibrationComponent> ent,
        ref TelecomCalibrationFinishedEvent args)
    {
        if (ent.Comp.MaintenanceFree || !IsIntegratedProcessorActive(ent.Owner))
            return;

        if (args.Cancelled || args.Handled)
            return;

        ent.Comp.Calibration = 100f;
        args.Handled = true;
        _popup.PopupEntity(
            Loc.GetString("telecom-calibration-complete"),
            ent.Owner,
            args.User,
            PopupType.Medium);
    }

    private void OnCalibrationDamaged(
        Entity<TelecomCalibrationComponent> ent,
        ref DamageDealtEvent args)
    {
        if (ent.Comp.MaintenanceFree || !IsIntegratedProcessorActive(ent.Owner))
            return;

        ent.Comp.Calibration = Math.Max(
            0f,
            ent.Comp.Calibration -
            args.Damage.GetTotal().Float() * ent.Comp.DamageLossMultiplier);
    }

    private void OnCalibrationEmp(Entity<TelecomCalibrationComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.MaintenanceFree || !IsIntegratedProcessorActive(ent.Owner))
            return;

        var loss = Math.Clamp(args.EnergyConsumption / 1000f * 15f, 5f, 25f);
        ent.Comp.Calibration = Math.Max(0f, ent.Comp.Calibration - loss);
        args.Affected = true;
    }

    private void OnCalibrationExamined(Entity<TelecomCalibrationComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !IsIntegratedProcessorActive(ent.Owner))
            return;

        var status = ent.Comp.Calibration switch
        {
            >= 90f => "nominal",
            >= 70f => "drifting",
            >= 40f => "poor",
            _ => "critical",
        };

        args.PushMarkup(Loc.GetString(
            $"telecom-calibration-examine-{status}",
            ("calibration", (int) MathF.Round(ent.Comp.Calibration))));
    }

    private void OnWearInteract(Entity<TelecomWearComponent> ent, ref InteractUsingEvent args)
    {
        if (ent.Comp.MaintenanceFree || args.Handled || !_tools.HasQuality(args.Used, WeldingQuality))
            return;

        if (TryComp<WiresPanelComponent>(ent.Owner, out var panel) && !panel.Open)
        {
            _popup.PopupEntity(
                Loc.GetString("telecom-maintenance-panel-closed"),
                ent.Owner,
                args.User,
                PopupType.Small);
            return;
        }

        args.Handled = _tools.UseTool(
            args.Used,
            args.User,
            ent.Owner,
            8f,
            WeldingQuality,
            new TelecomMaintenanceFinishedEvent(),
            1f);
    }

    private void OnMaintenanceFinished(
        Entity<TelecomWearComponent> ent,
        ref TelecomMaintenanceFinishedEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        ent.Comp.Condition = 100f;
        args.Handled = true;
        _popup.PopupEntity(
            Loc.GetString("telecom-maintenance-complete"),
            ent.Owner,
            args.User,
            PopupType.Medium);
    }

    private void OnWearDamaged(Entity<TelecomWearComponent> ent, ref DamageDealtEvent args)
    {
        if (ent.Comp.MaintenanceFree)
            return;

        ent.Comp.Condition = Math.Max(
            0f,
            ent.Comp.Condition -
            args.Damage.GetTotal().Float() * ent.Comp.DamageLossMultiplier);
    }

    private void OnWearEmp(Entity<TelecomWearComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.MaintenanceFree)
            return;

        var loss = Math.Clamp(args.EnergyConsumption / 1000f * 6f, 2f, 10f);
        ent.Comp.Condition = Math.Max(0f, ent.Comp.Condition - loss);
        args.Affected = true;
    }

    private void OnWearExamined(Entity<TelecomWearComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var status = ent.Comp.Condition switch
        {
            >= 90f => "nominal",
            >= 70f => "worn",
            >= 40f => "poor",
            _ => "critical",
        };

        args.PushMarkup(Loc.GetString(
            $"telecom-wear-examine-{status}",
            ("condition", (int) MathF.Round(ent.Comp.Condition))));
    }

    private void OnSolarFlare(ref TelecomSolarFlareEvent args)
    {
        var query = EntityQueryEnumerator<TelecomCalibrationComponent>();
        while (query.MoveNext(out var uid, out var calibration))
        {
            if (calibration.MaintenanceFree || !IsIntegratedProcessorActive(uid))
                continue;

            var variance = _random.NextFloat(0.8f, 1.2f);
            calibration.Calibration = Math.Max(
                0f,
                calibration.Calibration - args.CalibrationLoss * variance);
        }
    }
}

public readonly record struct TelecomRouteResult(bool CanBroadcast, string Message);

public readonly record struct TelecomTrafficMetrics(
    float Quality,
    float MaxUtilization,
    int LatencyMilliseconds)
{
    public static readonly TelecomTrafficMetrics Empty = new(0f, 0f, 0);
}

internal readonly record struct TelecomChainSnapshot(
    List<EntityUid> Receivers,
    List<EntityUid> Processors,
    List<EntityUid> Hubs,
    EntityUid Bus,
    EntityUid Server,
    List<EntityUid> Broadcasters,
    bool Standalone = false);

internal readonly record struct TelecomSelectedRoute(
    EntityUid Receiver,
    EntityUid Processor,
    EntityUid Broadcaster,
    EntityUid? InputHub,
    EntityUid? OutputHub);

internal readonly record struct TelecomPoolMetrics(float Quality, float Utilization);

internal readonly record struct TelecomNetworkLocation(MapId Map, EntityUid? Grid);

internal sealed class TelecomTransmissionContext(MapId sourceMap, int messageLength)
{
    public readonly MapId SourceMap = sourceMap;
    public readonly int MessageLength = messageLength;
    public readonly Dictionary<MapId, bool> Destinations = new();
}
