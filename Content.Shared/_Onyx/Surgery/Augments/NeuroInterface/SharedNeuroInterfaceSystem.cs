using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared._Onyx.Surgery.Augments;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Surgery.Augments.NeuroInterface;

public sealed partial class SharedNeuroInterfaceSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private AugmentModuleSystem _modules = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NeuroInterfaceComponent, OrganGotInsertedEvent>(OnInterfaceInserted);
        SubscribeLocalEvent<NeuroInterfaceComponent, OrganGotRemovedEvent>(OnInterfaceRemoved);
        SubscribeLocalEvent<NeuroInterfaceConsumerComponent, OrganGotInsertedEvent>(OnConsumerInserted);
        SubscribeLocalEvent<NeuroInterfaceConsumerComponent, OrganGotRemovedEvent>(OnConsumerRemoved);
        SubscribeLocalEvent<NeuroInterfaceComponent, AugmentModulesChangedEvent>(OnModulesChanged);
        SubscribeLocalEvent<NeuroInterfaceComponent, ExaminedEvent>(OnInterfaceExamined);
    }

    public bool IsEnabled(EntityUid body, EntityUid augment)
    {
        if (!HasComp<NeuroInterfaceConsumerComponent>(augment))
            return true;

        return !TryComp(augment, out NeuroInterfaceRuntimeComponent? runtime) || runtime.ManuallyEnabled;
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

    public IEnumerable<EntityUid> GetModules(EntityUid neuroInterface) => _modules.GetModules(neuroInterface);

    public IEnumerable<EntityUid> GetDirectModules(EntityUid host) => _modules.GetDirectModules(host);

    public IEnumerable<EntityUid> GetConsumers(EntityUid body)
    {
        var consumers = new HashSet<EntityUid>();
        if (TryComp(body, out InstalledAugmentsComponent? installed))
        {
            foreach (var netEntity in installed.Augments)
            {
                var uid = GetEntity(netEntity);
                if (HasComp<NeuroInterfaceConsumerComponent>(uid) && consumers.Add(uid))
                    yield return uid;
            }
        }

        var bodySystem = EntityManager.System<SharedBodySystem>();
        foreach (var (part, _) in bodySystem.GetBodyChildren(body))
        {
            if (HasComp<NeuroInterfaceConsumerComponent>(part) && consumers.Add(part))
                yield return part;
        }
        foreach (var (organ, _) in bodySystem.GetBodyOrgans(body))
        {
            if (HasComp<NeuroInterfaceConsumerComponent>(organ) && consumers.Add(organ))
                yield return organ;
        }

        if (!TryGetInterface(body, out var neuroInterface))
            yield break;

        foreach (var module in _modules.GetModules(neuroInterface))
        {
            if (HasComp<NeuroInterfaceConsumerComponent>(module) && consumers.Add(module))
                yield return module;
        }
    }

    public bool IsConsumerOperational(EntityUid consumer) =>
        !HasComp<EmpDisabledComponent>(consumer) &&
        (!TryComp(consumer, out CyberneticsComponent? cybernetics) || !cybernetics.Disabled);

    public void Refresh(EntityUid body)
    {
        if (_net.IsClient || !TryComp(body, out InstalledAugmentsComponent? _))
            return;

        foreach (var uid in GetConsumers(body))
            EnsureComp<NeuroInterfaceRuntimeComponent>(uid);
    }

    public void SetConsumer(EntityUid body, EntityUid augment, bool enabled)
    {
        if (_net.IsClient || !GetConsumers(body).Contains(augment))
            return;

        var runtime = EnsureComp<NeuroInterfaceRuntimeComponent>(augment);
        if (runtime.ManuallyEnabled == enabled)
            return;

        runtime.ManuallyEnabled = enabled;
        Dirty(augment, runtime);
        var changed = new NeuroInterfaceEnabledChangedEvent(enabled);
        RaiseLocalEvent(augment, ref changed);
    }

    private void OnInterfaceInserted(Entity<NeuroInterfaceComponent> ent, ref OrganGotInsertedEvent args) => Refresh(args.Target);
    private void OnInterfaceRemoved(Entity<NeuroInterfaceComponent> ent, ref OrganGotRemovedEvent args) => Refresh(args.Target);
    private void OnConsumerInserted(Entity<NeuroInterfaceConsumerComponent> ent, ref OrganGotInsertedEvent args) => Refresh(args.Target);
    private void OnConsumerRemoved(Entity<NeuroInterfaceConsumerComponent> ent, ref OrganGotRemovedEvent args) => Refresh(args.Target);
    private void OnModulesChanged(Entity<NeuroInterfaceComponent> ent, ref AugmentModulesChangedEvent args) => RefreshInterface(ent);

    private void OnInterfaceExamined(Entity<NeuroInterfaceComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(NeuroInterfaceComponent)))
        {
            args.PushMarkup(Loc.GetString("neuro-interface-examine-expansion-modules",
                ("count", GetModules(ent).Count())));
        }
    }

    private void RefreshInterface(Entity<NeuroInterfaceComponent> ent)
    {
        if (CompOrNull<OrganComponent>(ent)?.Body is { } body)
            Refresh(body);
    }

}
