using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Emp;
using Content.Shared.FixedPoint;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell.Components;
using Content.Shared._Onyx.Body.Systems;
using Content.Shared._Onyx.Surgery.Augments;
using Content.Shared._Onyx.Surgery.Augments.NeuroInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Surgery.Augments.NeuroInterface;

public sealed partial class NeuroInterfaceSystem : EntitySystem
{
    [Dependency] private SharedNeuroInterfaceSystem _neuro = default!;
    [Dependency] private AugmentSystem _augment = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private OrganHealthSystem _organHealth = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NeuroInterfaceComponent, BoundUIOpenedEvent>(OnUiOpened);
        Subs.BuiEvents<NeuroInterfaceComponent>(NeuroInterfaceUiKey.Key, subs =>
        {
            subs.Event<NeuroInterfaceSetModeMessage>(OnSetMode);
            subs.Event<NeuroInterfaceSetEnabledMessage>(OnSetEnabled);
            subs.Event<NeuroInterfaceSetRoutingMessage>(OnSetRouting);
        });
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<NeuroInterfaceComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var neuroInterface, out var organ))
        {
            if (organ.Body is not { } body || _timing.CurTime < neuroInterface.NextUpdate)
                continue;
            var updateInterval = neuroInterface.UpdateInterval > TimeSpan.Zero
                ? neuroInterface.UpdateInterval
                : TimeSpan.FromSeconds(1);
            neuroInterface.NextUpdate = _timing.CurTime + updateInterval;
            if (_ui.IsUiOpen(uid, NeuroInterfaceUiKey.Key))
                UpdateUi((uid, neuroInterface), body);
            if (neuroInterface.Mode != NeuroInterfaceMode.Overclock || HasComp<EmpDisabledComponent>(uid))
                continue;

            var capacity = _neuro.GetCapacity(uid, neuroInterface);
            var channelCapacity = _neuro.GetChannelCapacity(uid, neuroInterface);
            var (demand, channels) = GetEnabledLoad(body);
            if (demand <= capacity && channels <= channelCapacity)
                continue;
            var multiplier = _neuro.GetCapacityMultiplier(neuroInterface);
            var forcedLoad = Math.Min(demand, capacity * multiplier);
            var loadRatio = capacity > 0f ? forcedLoad / capacity : multiplier;
            var channelRatio = channelCapacity > 0
                ? Math.Min(channels / (float) channelCapacity, multiplier)
                : multiplier;
            var damage = CalculateOverclockDamage(neuroInterface, loadRatio, channelRatio) * (float) updateInterval.TotalSeconds;
            _organHealth.ChangeHealth((uid, organ), -FixedPoint2.New(damage));
            if (FindBrain(body) is { } brain && TryComp(brain, out OrganComponent? brainOrgan))
                _organHealth.ChangeHealth((brain, brainOrgan), -FixedPoint2.New(damage));
        }
    }

    private void OnUiOpened(Entity<NeuroInterfaceComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (GetOwner(ent.Owner) is not { } body || body != args.Actor)
        {
            _ui.CloseUi(ent.Owner, NeuroInterfaceUiKey.Key, args.Actor);
            return;
        }
        UpdateUi(ent, body);
    }

    private void OnSetMode(Entity<NeuroInterfaceComponent> ent, ref NeuroInterfaceSetModeMessage args)
    {
        if (!ValidateOwner(ent, args.Actor, out var body) || !Enum.IsDefined(args.Mode))
            return;
        ent.Comp.Mode = args.Mode;
        Dirty(ent);
        _neuro.Refresh(body);
        UpdateUi(ent, body);
    }

    private void OnSetEnabled(Entity<NeuroInterfaceComponent> ent, ref NeuroInterfaceSetEnabledMessage args)
    {
        if (!ValidateOwner(ent, args.Actor, out var body))
            return;
        var augment = GetEntity(args.Augment);
        _neuro.SetConsumer(body, augment, args.Enabled);
        UpdateUi(ent, body);
    }

    private void OnSetRouting(Entity<NeuroInterfaceComponent> ent, ref NeuroInterfaceSetRoutingMessage args)
    {
        if (!ValidateOwner(ent, args.Actor, out var body))
            return;
        _neuro.SetRouting(body, GetEntity(args.Augment), args.Action);
        UpdateUi(ent, body);
    }

    private bool ValidateOwner(Entity<NeuroInterfaceComponent> ent, EntityUid actor, out EntityUid body)
    {
        body = GetOwner(ent.Owner) ?? default;
        return body == actor && _ui.IsUiOpen(ent.Owner, NeuroInterfaceUiKey.Key, actor);
    }

    private EntityUid? GetOwner(EntityUid neuroInterface) => CompOrNull<OrganComponent>(neuroInterface)?.Body;

    private void UpdateUi(Entity<NeuroInterfaceComponent> ent, EntityUid body)
    {
        var consumers = new List<NeuroInterfaceEntryData>();
        var totalDemand = 0f;
        var enabledChannels = 0;
        var empDisabled = HasComp<EmpDisabledComponent>(ent);
        var routerCapacity = _neuro.GetRouterCapacity(ent);
        var routedCount = 0;
        foreach (var uid in _neuro.GetConsumers(body))
        {
            if (!TryComp(uid, out NeuroBandwidthConsumerComponent? consumer) ||
                !TryComp(uid, out NeuroBandwidthRuntimeComponent? runtime))
                continue;
            var operational = _neuro.IsConsumerOperational(uid);
            if (runtime.ManuallyEnabled && operational)
            {
                totalDemand += _neuro.GetDemand(consumer);
                enabledChannels++;
            }
            var routed = runtime.Routed && runtime.RoutingOrder < routerCapacity;
            if (routed)
                routedCount++;
            var status = empDisabled || !operational ? NeuroConsumerStatus.Emp
                : !runtime.ManuallyEnabled ? NeuroConsumerStatus.Disabled
                : runtime.Efficiency <= 0f ? NeuroConsumerStatus.Offline
                : runtime.Efficiency >= 1f ? NeuroConsumerStatus.Full
                : NeuroConsumerStatus.Throttled;
            var tooltip = new CollectNeuroInterfaceTooltipEvent();
            RaiseLocalEvent(uid, tooltip);
            consumers.Add(new NeuroInterfaceEntryData(GetNetEntity(uid), Name(uid), MetaData(uid).EntityDescription, consumer.Demand,
                CompOrNull<AugmentPowerDrawComponent>(uid)?.Draw ?? 0f,
                runtime.ManuallyEnabled, routed, runtime.RoutingOrder, runtime.Efficiency,
                status, consumer.Scalable, GetRegion(uid),
                true, true, true, tooltip.Sections));
        }
        var interfaceTooltip = new CollectNeuroInterfaceTooltipEvent();
        RaiseLocalEvent(ent.Owner, interfaceTooltip);
        consumers.Add(new NeuroInterfaceEntryData(
            GetNetEntity(ent.Owner),
            Name(ent.Owner),
            MetaData(ent.Owner).EntityDescription,
            0f,
            0f,
            true,
            false,
            0,
            empDisabled ? 0f : 1f,
            empDisabled ? NeuroConsumerStatus.Emp : NeuroConsumerStatus.Full,
            false,
            NeuroInterfaceBodyRegion.Head,
            false,
            false,
            false,
            interfaceTooltip.Sections));
        consumers.Sort((left, right) =>
        {
            var routed = right.Routed.CompareTo(left.Routed);
            return routed != 0 ? routed : left.RoutingOrder.CompareTo(right.RoutingOrder);
        });
        var capacity = _neuro.GetCapacity(ent, ent.Comp);
        var channelCapacity = _neuro.GetChannelCapacity(ent, ent.Comp);
        var chip = _slots.GetItemOrNull(ent.Owner, NeuroInterfaceComponent.ChipSlotId);
        var chipComponent = chip is { } chipUid ? CompOrNull<NeuroInterfaceChipComponent>(chipUid) : null;
        var cache = _slots.GetItemOrNull(ent.Owner, NeuroInterfaceComponent.CacheSlotId);
        var cacheComponent = cache is { } cacheUid ? CompOrNull<NeuroInterfaceCacheComponent>(cacheUid) : null;
        var batteries = new List<NeuroInterfaceBatteryData>();
        var netPower = 0f;
        foreach (var battery in _augment.GetBatteries(body))
        {
            var charge = _battery.GetCharge(battery.AsNullable());
            netPower += battery.Comp.ChargeRate;
            batteries.Add(new NeuroInterfaceBatteryData(Name(battery), charge, battery.Comp.MaxCharge, battery.Comp.ChargeRate));
        }
        var sources = new List<string>();
        var reactorGeneration = 0f;
        if (TryComp(body, out InstalledAugmentsComponent? installed))
        {
            foreach (var source in _augment.ResolveAugments(installed))
            {
                if (HasComp<AugmentPowerSourceComponent>(source))
                    sources.Add(Name(source));
                if (TryComp(source, out AugmentReactorComponent? reactor))
                    reactorGeneration += reactor.CurrentGeneration;
            }
        }
        var consumption = _augment.GetPowerSlots(body)
            .Sum(slot => CompOrNull<PowerCellDrawComponent>(slot)?.DrawRate ?? 0f);
        var generation = reactorGeneration + Math.Max(0f, netPower + consumption);
        _ui.SetUiState(ent.Owner, NeuroInterfaceUiKey.Key, new NeuroInterfaceBuiState(
            ent.Comp.Mode,
            capacity,
            totalDemand,
            Math.Max(0f, totalDemand - capacity),
            Math.Max(0, enabledChannels - channelCapacity),
            enabledChannels,
            channelCapacity,
            GetSlotName(ent, NeuroInterfaceComponent.ChipSlotId),
            GetSlotName(ent, NeuroInterfaceComponent.CacheSlotId),
            GetSlotName(ent, NeuroInterfaceComponent.RouterSlotId),
            chipComponent?.Bandwidth ?? 0f,
            chipComponent?.Channels ?? 0,
            cacheComponent?.Channels ?? 0,
            routerCapacity,
            Math.Min(routedCount, routerCapacity),
            _neuro.GetModules(ent).Select(module => Name(module)).ToList(),
            batteries,
            sources,
            generation,
            consumption,
            consumers));
    }

    private (float Demand, int Channels) GetEnabledLoad(EntityUid body)
    {
        var demand = 0f;
        var channels = 0;
        foreach (var uid in _neuro.GetConsumers(body))
        {
            if (!_neuro.IsConsumerOperational(uid) ||
                !TryComp(uid, out NeuroBandwidthRuntimeComponent? runtime) ||
                !runtime.ManuallyEnabled ||
                !TryComp(uid, out NeuroBandwidthConsumerComponent? consumer))
                continue;

            demand += _neuro.GetDemand(consumer);
            channels++;
        }

        return (demand, channels);
    }

    private EntityUid? FindBrain(EntityUid body)
    {
        foreach (var (organ, _) in _body.GetBodyOrgans(body))
        {
            if (HasComp<BrainComponent>(organ))
                return organ;
        }
        return null;
    }

    private string? GetSlotName(EntityUid neuroInterface, string slot) =>
        _slots.GetItemOrNull(neuroInterface, slot) is { } item ? Name(item) : null;

    private NeuroInterfaceBodyRegion GetRegion(EntityUid augment)
    {
        if (!TryComp(Transform(augment).ParentUid, out BodyPartComponent? part))
            return NeuroInterfaceBodyRegion.Other;

        return (part.PartType, part.Symmetry) switch
        {
            (BodyPartType.Head, _) => NeuroInterfaceBodyRegion.Head,
            (BodyPartType.Chest or BodyPartType.Torso, _) => NeuroInterfaceBodyRegion.Chest,
            (BodyPartType.Groin, _) => NeuroInterfaceBodyRegion.Groin,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => NeuroInterfaceBodyRegion.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => NeuroInterfaceBodyRegion.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => NeuroInterfaceBodyRegion.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => NeuroInterfaceBodyRegion.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => NeuroInterfaceBodyRegion.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => NeuroInterfaceBodyRegion.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => NeuroInterfaceBodyRegion.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => NeuroInterfaceBodyRegion.RightFoot,
            _ => NeuroInterfaceBodyRegion.Other,
        };
    }

    private static float CalculateOverclockDamage(
        NeuroInterfaceComponent component,
        float loadRatio,
        float channelRatio) => Math.Clamp(
        (Math.Max(loadRatio, channelRatio) - 1f) * SanitizeNonNegative(component.OverclockDamageCoefficient),
        SanitizeNonNegative(component.MinimumOverclockDamage),
        Math.Max(SanitizeNonNegative(component.MinimumOverclockDamage),
            SanitizeNonNegative(component.MaximumOverclockDamage)));

    private static float SanitizeNonNegative(float value) => float.IsFinite(value) ? Math.Max(0f, value) : 0f;
}
