using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Emp;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell.Components;
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
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NeuroInterfaceComponent, BoundUIOpenedEvent>(OnUiOpened);
        Subs.BuiEvents<NeuroInterfaceComponent>(NeuroInterfaceUiKey.Key, subs =>
        {
            subs.Event<NeuroInterfaceSetEnabledMessage>(OnSetEnabled);
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

    private void OnSetEnabled(Entity<NeuroInterfaceComponent> ent, ref NeuroInterfaceSetEnabledMessage args)
    {
        if (!ValidateOwner(ent, args.Actor, out var body))
            return;
        _neuro.SetConsumer(body, GetEntity(args.Augment), args.Enabled);
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
        var entries = new List<NeuroInterfaceEntryData>();
        if (TryComp(body, out InstalledAugmentsComponent? installed))
        {
            foreach (var augment in _augment.ResolveAugments(installed))
                AddEntryTree(entries, augment, null, GetRegion(augment));
        }

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
        if (installed != null)
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
            _neuro.GetModules(ent).Select(module => Name(module)).ToList(),
            batteries,
            sources,
            generation,
            consumption,
            entries));
    }

    private void AddEntryTree(
        List<NeuroInterfaceEntryData> entries,
        EntityUid uid,
        EntityUid? parent,
        NeuroInterfaceBodyRegion region)
    {
        var runtime = CompOrNull<NeuroInterfaceRuntimeComponent>(uid);
        var operational = _neuro.IsConsumerOperational(uid);
        var enabled = runtime?.ManuallyEnabled ?? true;
        var status = !operational
            ? NeuroConsumerStatus.Emp
            : enabled
                ? NeuroConsumerStatus.Full
                : NeuroConsumerStatus.Disabled;
        var tooltip = new CollectNeuroInterfaceTooltipEvent();
        RaiseLocalEvent(uid, tooltip);
        entries.Add(new NeuroInterfaceEntryData(
            GetNetEntity(uid),
            parent is { } parentUid ? GetNetEntity(parentUid) : null,
            Name(uid),
            MetaData(uid).EntityDescription,
            CompOrNull<AugmentPowerDrawComponent>(uid)?.Draw ?? 0f,
            enabled,
            status,
            region,
            HasComp<NeuroInterfaceConsumerComponent>(uid),
            tooltip.Sections));

        foreach (var module in _neuro.GetDirectModules(uid))
            AddEntryTree(entries, module, uid, region);
    }

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
}
