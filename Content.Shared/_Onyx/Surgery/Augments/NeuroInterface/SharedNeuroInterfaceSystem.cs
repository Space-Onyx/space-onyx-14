using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared._Onyx.Surgery.Augments;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Surgery.Augments.NeuroInterface;

public sealed partial class SharedNeuroInterfaceSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NeuroInterfaceComponent, OrganGotInsertedEvent>(OnInterfaceInserted);
        SubscribeLocalEvent<NeuroInterfaceComponent, OrganGotRemovedEvent>(OnInterfaceRemoved);
        SubscribeLocalEvent<NeuroBandwidthConsumerComponent, OrganGotInsertedEvent>(OnConsumerInserted);
        SubscribeLocalEvent<NeuroBandwidthConsumerComponent, OrganGotRemovedEvent>(OnConsumerRemoved);
        SubscribeLocalEvent<NeuroInterfaceComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<NeuroInterfaceComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<NeuroInterfaceComponent, EmpPulseEvent>(OnEmp);
        SubscribeLocalEvent<NeuroInterfaceComponent, EmpDisabledRemovedEvent>(OnEmpRemoved);
        SubscribeLocalEvent<InstalledAugmentsComponent, CyberneticsEmpProtectionEvent>(OnEmpProtection);
        SubscribeLocalEvent<NeuroInterfaceComponent, ExaminedEvent>(OnInterfaceExamined);
        SubscribeLocalEvent<NeuroInterfaceChipComponent, ExaminedEvent>(OnChipExamined);
        SubscribeLocalEvent<NeuroInterfaceCacheComponent, ExaminedEvent>(OnCacheExamined);
        SubscribeLocalEvent<NeuroInterfaceRouterComponent, ExaminedEvent>(OnRouterExamined);
        SubscribeLocalEvent<NeuroInterfaceModuleComponent, ExaminedEvent>(OnModuleExamined);
        SubscribeLocalEvent<NeuroBandwidthConsumerComponent, ExaminedEvent>(OnConsumerExamined);
    }

    public float GetEfficiency(EntityUid body, EntityUid augment)
    {
        if (!TryGetInterface(body, out _))
            return 0f;

        return TryComp(augment, out NeuroBandwidthRuntimeComponent? runtime) ? runtime.Efficiency : 1f;
    }

    public float GetCapacity(EntityUid neuroInterface, NeuroInterfaceComponent component)
    {
        var capacity = Math.Max(0f, component.BaseBandwidth);
        if (_slots.GetItemOrNull(neuroInterface, NeuroInterfaceComponent.ChipSlotId) is { } chip &&
            TryComp(chip, out NeuroInterfaceChipComponent? chipComponent))
            capacity += Math.Max(0f, chipComponent.Bandwidth);
        return capacity;
    }

    public int GetChannelCapacity(EntityUid neuroInterface, NeuroInterfaceComponent component)
    {
        var channels = Math.Max(1, component.BaseChannels);
        if (_slots.GetItemOrNull(neuroInterface, NeuroInterfaceComponent.ChipSlotId) is { } chip &&
            TryComp(chip, out NeuroInterfaceChipComponent? chipComponent))
            channels += Math.Max(0, chipComponent.Channels);
        if (_slots.GetItemOrNull(neuroInterface, NeuroInterfaceComponent.CacheSlotId) is { } cache &&
            TryComp(cache, out NeuroInterfaceCacheComponent? cacheComponent))
            channels += Math.Max(0, cacheComponent.Channels);

        return channels;
    }

    public int GetRouterCapacity(EntityUid neuroInterface)
    {
        if (_slots.GetItemOrNull(neuroInterface, NeuroInterfaceComponent.RouterSlotId) is not { } router ||
            !TryComp(router, out NeuroInterfaceRouterComponent? component))
            return 0;

        return Math.Max(1, component.Capacity);
    }

    public bool TryGetInterface(EntityUid body, out Entity<NeuroInterfaceComponent> neuroInterface)
    {
        neuroInterface = default;
        if (!TryComp(body, out InstalledAugmentsComponent? installed))
            return false;

        foreach (var netEntity in installed.Augments)
        {
            var uid = GetEntity(netEntity);
            if (TryComp(uid, out NeuroInterfaceComponent? component))
            {
                neuroInterface = (uid, component);
                return true;
            }
        }
        return false;
    }

    public IEnumerable<EntityUid> GetModules(EntityUid neuroInterface)
    {
        if (!TryComp(neuroInterface, out ItemSlotsComponent? slots))
            yield break;

        foreach (var slot in slots.Slots.Values)
        {
            if (slot.Item is { } item && HasComp<NeuroInterfaceModuleComponent>(item))
                yield return item;
        }
    }

    public IEnumerable<EntityUid> GetConsumers(EntityUid body)
    {
        var consumers = new HashSet<EntityUid>();
        if (TryComp(body, out InstalledAugmentsComponent? installed))
        {
            foreach (var netEntity in installed.Augments)
            {
                var uid = GetEntity(netEntity);
                if (HasComp<NeuroBandwidthConsumerComponent>(uid) && consumers.Add(uid))
                    yield return uid;
            }
        }

        var bodySystem = EntityManager.System<SharedBodySystem>();
        foreach (var (part, _) in bodySystem.GetBodyChildren(body))
        {
            if (HasComp<NeuroBandwidthConsumerComponent>(part) && consumers.Add(part))
                yield return part;
        }
        foreach (var (organ, _) in bodySystem.GetBodyOrgans(body))
        {
            if (HasComp<NeuroBandwidthConsumerComponent>(organ) && consumers.Add(organ))
                yield return organ;
        }
    }

    public void Refresh(EntityUid body)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(body, out InstalledAugmentsComponent? installed))
            return;

        var consumers = new List<(EntityUid Uid, NeuroBandwidthConsumerComponent Consumer, NeuroBandwidthRuntimeComponent Runtime)>();
        foreach (var uid in GetConsumers(body))
        {
            var consumer = Comp<NeuroBandwidthConsumerComponent>(uid);
            consumers.Add((uid, consumer, EnsureComp<NeuroBandwidthRuntimeComponent>(uid)));
        }

        if (!TryGetInterface(body, out var neuroInterface))
        {
            SetEfficiencies(consumers, 0f);
            return;
        }

        if (HasComp<EmpDisabledComponent>(neuroInterface) ||
            TryComp(neuroInterface, out CyberneticsComponent? cybernetics) && cybernetics.Disabled)
        {
            SetEfficiencies(consumers, 0f);
            return;
        }

        var capacity = GetCapacity(neuroInterface, neuroInterface.Comp);
        var channels = GetChannelCapacity(neuroInterface, neuroInterface.Comp);
        var routerCapacity = GetRouterCapacity(neuroInterface);
        NormalizeRouting(consumers, routerCapacity);
        var availableChannels = neuroInterface.Comp.Mode == NeuroInterfaceMode.Overclock
            ? (int) MathF.Ceiling(channels * 1.5f)
            : channels;
        var enabledDemand = consumers.Where(entry => entry.Runtime.ManuallyEnabled)
            .Sum(entry => Math.Max(0f, entry.Consumer.Demand));
        var budget = neuroInterface.Comp.Mode == NeuroInterfaceMode.Overclock && enabledDemand <= capacity * 1.5f
            ? enabledDemand
            : capacity * (neuroInterface.Comp.Mode == NeuroInterfaceMode.Overclock ? 1.5f : 1f);

        var channel = 0;
        foreach (var entry in consumers
                     .OrderBy(entry => !entry.Runtime.Routed)
                     .ThenBy(entry => entry.Runtime.RoutingOrder))
        {
            var demand = Math.Max(0f, entry.Consumer.Demand);
            var available = demand <= 0f ? 1f : Math.Clamp(budget / demand, 0f, 1f);
            var hasChannel = entry.Runtime.ManuallyEnabled && channel < availableChannels;
            if (entry.Runtime.ManuallyEnabled)
                channel++;
            var efficiency = !hasChannel
                ? 0f
                : demand <= 0f
                    ? 1f
                    : entry.Consumer.Scalable
                        ? available
                        : available >= 1f ? 1f : 0f;
            budget = Math.Max(0f, budget - demand * efficiency);
            SetEfficiency(entry.Uid, entry.Runtime, efficiency);
        }
    }

    public void SetConsumer(EntityUid body, EntityUid augment, bool enabled)
    {
        if (_net.IsClient)
            return;

        if (!GetConsumers(body).Contains(augment))
            return;

        var runtime = EnsureComp<NeuroBandwidthRuntimeComponent>(augment);
        runtime.ManuallyEnabled = enabled;
        Dirty(augment, runtime);
        Refresh(body);
    }

    public void SetRouting(EntityUid body, EntityUid augment, NeuroRoutingAction action)
    {
        if (_net.IsClient || !Enum.IsDefined(action) || !TryGetInterface(body, out var neuroInterface))
            return;

        var capacity = GetRouterCapacity(neuroInterface);
        if (capacity <= 0 || !GetConsumers(body).Contains(augment))
            return;

        var routed = GetConsumers(body)
            .Where(uid => TryComp(uid, out NeuroBandwidthRuntimeComponent? runtime) && runtime.Routed)
            .OrderBy(uid => Comp<NeuroBandwidthRuntimeComponent>(uid).RoutingOrder)
            .ToList();
        NormalizeRouting(routed);

        var runtime = EnsureComp<NeuroBandwidthRuntimeComponent>(augment);
        var index = routed.IndexOf(augment);
        switch (action)
        {
            case NeuroRoutingAction.Add when index < 0 && routed.Count < capacity:
                runtime.Routed = true;
                runtime.RoutingOrder = routed.Count;
                Dirty(augment, runtime);
                routed.Add(augment);
                break;
            case NeuroRoutingAction.Remove when index >= 0:
                runtime.Routed = false;
                runtime.RoutingOrder = 0;
                Dirty(augment, runtime);
                routed.RemoveAt(index);
                break;
            case NeuroRoutingAction.MoveUp when index > 0:
                (routed[index - 1], routed[index]) = (routed[index], routed[index - 1]);
                break;
            case NeuroRoutingAction.MoveDown when index >= 0 && index < routed.Count - 1:
                (routed[index + 1], routed[index]) = (routed[index], routed[index + 1]);
                break;
            default:
                return;
        }

        NormalizeRouting(routed);
        Refresh(body);
    }

    private void SetEfficiencies(List<(EntityUid Uid, NeuroBandwidthConsumerComponent Consumer, NeuroBandwidthRuntimeComponent Runtime)> consumers, float efficiency)
    {
        foreach (var entry in consumers)
            SetEfficiency(entry.Uid, entry.Runtime, entry.Runtime.ManuallyEnabled ? efficiency : 0f);
    }

    private void SetEfficiency(EntityUid uid, NeuroBandwidthRuntimeComponent runtime, float efficiency)
    {
        if (MathHelper.CloseTo(runtime.Efficiency, efficiency))
            return;
        runtime.Efficiency = efficiency;
        Dirty(uid, runtime);
        var changed = new NeuroBandwidthEfficiencyChangedEvent(efficiency);
        RaiseLocalEvent(uid, ref changed);
    }

    private void OnInterfaceInserted(Entity<NeuroInterfaceComponent> ent, ref OrganGotInsertedEvent args) => Refresh(args.Target);
    private void OnInterfaceRemoved(Entity<NeuroInterfaceComponent> ent, ref OrganGotRemovedEvent args) => Refresh(args.Target);
    private void OnConsumerInserted(Entity<NeuroBandwidthConsumerComponent> ent, ref OrganGotInsertedEvent args) => Refresh(args.Target);
    private void OnConsumerRemoved(Entity<NeuroBandwidthConsumerComponent> ent, ref OrganGotRemovedEvent args) => Refresh(args.Target);
    private void OnModuleInserted(Entity<NeuroInterfaceComponent> ent, ref EntInsertedIntoContainerMessage args) => RefreshInterface(ent);
    private void OnModuleRemoved(Entity<NeuroInterfaceComponent> ent, ref EntRemovedFromContainerMessage args) => RefreshInterface(ent);

    private void OnEmp(Entity<NeuroInterfaceComponent> ent, ref EmpPulseEvent args)
    {
        if (CompOrNull<OrganComponent>(ent)?.Body is not { } body)
            return;
        SetInterfaceConsumers(body, 0f);
    }

    private void OnEmpRemoved(Entity<NeuroInterfaceComponent> ent, ref EmpDisabledRemovedEvent args) => RefreshInterface(ent);

    private void OnEmpProtection(Entity<InstalledAugmentsComponent> body, ref CyberneticsEmpProtectionEvent args)
    {
        if (!TryGetInterface(body, out var neuroInterface) || args.Cybernetic != neuroInterface.Owner)
            return;

        foreach (var module in GetModules(neuroInterface))
        {
            if (!TryComp(module, out NeuroInterfaceEmpProtectionComponent? protection))
                continue;

            args.StrengthMultiplier *= Math.Max(0f, protection.StrengthMultiplier);
            args.DurationMultiplier *= Math.Max(0f, protection.DurationMultiplier);
        }
    }

    private void OnInterfaceExamined(Entity<NeuroInterfaceComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(NeuroInterfaceComponent)))
        {
            args.PushMarkup(Loc.GetString("neuro-interface-examine-base-bandwidth",
                ("bandwidth", ent.Comp.BaseBandwidth)));
            args.PushMarkup(Loc.GetString("neuro-interface-examine-total-bandwidth",
                ("bandwidth", GetCapacity(ent, ent.Comp))));
            args.PushMarkup(Loc.GetString("neuro-interface-examine-channels",
                ("channels", GetChannelCapacity(ent, ent.Comp))));
            args.PushMarkup(Loc.GetString("neuro-interface-examine-expansion-modules",
                ("count", GetModules(ent).Count())));
        }
    }

    private void OnChipExamined(Entity<NeuroInterfaceChipComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("neuro-interface-examine-chip",
                ("bandwidth", ent.Comp.Bandwidth), ("channels", ent.Comp.Channels)));
    }

    private void OnCacheExamined(Entity<NeuroInterfaceCacheComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("neuro-interface-examine-cache",
                ("channels", ent.Comp.Channels)));
    }

    private void OnRouterExamined(Entity<NeuroInterfaceRouterComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("neuro-interface-examine-router",
                ("capacity", Math.Max(1, ent.Comp.Capacity))));
    }

    private void OnModuleExamined(Entity<NeuroInterfaceModuleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("neuro-interface-examine-module"));
        if (TryComp(ent, out NeuroInterfaceEmpProtectionComponent? protection))
        {
            args.PushMarkup(Loc.GetString("neuro-interface-examine-emp-protection",
                ("strength", MathF.Round((1f - protection.StrengthMultiplier) * 100f)),
                ("duration", MathF.Round((1f - protection.DurationMultiplier) * 100f))));
        }
    }

    private void OnConsumerExamined(Entity<NeuroBandwidthConsumerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("neuro-interface-examine-consumer",
            ("demand", ent.Comp.Demand),
            ("power", CompOrNull<AugmentPowerDrawComponent>(ent)?.Draw ?? 0f)));
        args.PushMarkup(Loc.GetString(ent.Comp.Scalable
            ? "neuro-interface-examine-scalable"
            : "neuro-interface-examine-binary"));
    }

    private void RefreshInterface(Entity<NeuroInterfaceComponent> ent)
    {
        if (CompOrNull<OrganComponent>(ent)?.Body is { } body)
            Refresh(body);
    }

    private void SetInterfaceConsumers(EntityUid body, float efficiency)
    {
        foreach (var uid in GetConsumers(body))
        {
            if (TryComp(uid, out NeuroBandwidthRuntimeComponent? runtime))
                SetEfficiency(uid, runtime, runtime.ManuallyEnabled ? efficiency : 0f);
        }
    }

    private void NormalizeRouting(List<EntityUid> routed)
    {
        for (var i = 0; i < routed.Count; i++)
        {
            var runtime = Comp<NeuroBandwidthRuntimeComponent>(routed[i]);
            if (runtime.RoutingOrder == i)
                continue;
            runtime.RoutingOrder = i;
            Dirty(routed[i], runtime);
        }
    }

    private void NormalizeRouting(
        List<(EntityUid Uid, NeuroBandwidthConsumerComponent Consumer, NeuroBandwidthRuntimeComponent Runtime)> consumers,
        int capacity)
    {
        var routed = consumers
            .Where(entry => entry.Runtime.Routed)
            .OrderBy(entry => entry.Runtime.RoutingOrder)
            .ToList();
        for (var i = 0; i < routed.Count; i++)
        {
            var entry = routed[i];
            var enabled = i < capacity;
            if (entry.Runtime.Routed == enabled && (!enabled || entry.Runtime.RoutingOrder == i))
                continue;

            entry.Runtime.Routed = enabled;
            entry.Runtime.RoutingOrder = enabled ? i : 0;
            Dirty(entry.Uid, entry.Runtime);
        }
    }
}
