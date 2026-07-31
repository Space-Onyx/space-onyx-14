using Content.Shared._Onyx.Xenomorphs.Infection;
using Content.Shared._Onyx.Xenomorphs.Larva;
using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Systems;
using Robust.Server.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Xenomorphs.Infection;

public sealed partial class XenomorphInfectionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedEntityEffectsSystem _effect = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenomorphInfectionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<XenomorphInfectionComponent, OrganGotInsertedEvent>(OnOrganAddedToBody);
        SubscribeLocalEvent<XenomorphInfectionComponent, OrganGotRemovedEvent>(OnOrganRemovedFromBody);
    }

    private void OnShutdown(EntityUid uid, XenomorphInfectionComponent component, ComponentShutdown args)
    {
        if (component.Infected.HasValue)
            RemComp<XenomorphInfectedComponent>(component.Infected.Value);
    }

    private void OnOrganAddedToBody(EntityUid uid, XenomorphInfectionComponent component, OrganGotInsertedEvent args)
    {
        var xenomorphInfected = EnsureComp<XenomorphInfectedComponent>(args.Target);
        xenomorphInfected.Infection = uid;
        xenomorphInfected.InfectedIcons = component.InfectedIcons;
        Dirty(args.Target, xenomorphInfected);

        component.Infected = args.Target;
    }

    private void OnOrganRemovedFromBody(EntityUid uid, XenomorphInfectionComponent component, OrganGotRemovedEvent args)
    {
        RemComp<XenomorphPreventSuicideComponent>(args.Target);
        RemComp<XenomorphInfectedComponent>(args.Target);
        component.Infected = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;

        var query = EntityQueryEnumerator<XenomorphInfectionComponent>();
        while (query.MoveNext(out var uid, out var infection))
        {
            if (!infection.Infected.HasValue || infection.GrowthStage >= infection.MaxGrowthStage || time < infection.NextPointsAt)
                continue;

            infection.NextPointsAt = time + infection.GrowTime;

            if (_mobState.IsDead(infection.Infected.Value) || !_random.Prob(infection.GrowProb))
                continue;

            infection.GrowthStage++;
            if (TryComp<XenomorphInfectedComponent>(infection.Infected.Value, out var xenomorphInfected))
            {
                xenomorphInfected.GrowthStage = infection.GrowthStage;
                DirtyField(infection.Infected.Value, xenomorphInfected, nameof(XenomorphInfectedComponent.GrowthStage));
            }

            if (infection.Effects.TryGetValue(infection.GrowthStage, out var effects))
            {
                foreach (var effect in effects)
                    _effect.TryApplyEffect(infection.Infected.Value, effect);
            }

            if (infection.GrowthStage < infection.MaxGrowthStage)
                continue;

            if (!_container.TryGetContainingContainer((uid, null, null), out var container))
            {
                QueueDel(uid);
                continue;
            }

            var larva = Spawn(infection.LarvaPrototype);

            var larvaComponent = EnsureComp<XenomorphLarvaComponent>(larva);
            larvaComponent.Victim = infection.Infected.Value;

            var larvaVictim = EnsureComp<XenomorphLarvaVictimComponent>(infection.Infected.Value);
            if (infection.InfectedIcons.TryGetValue(infection.GrowthStage, out var infectedIcon))
            {
                larvaVictim.InfectedIcon = infectedIcon;
                Dirty(infection.Infected.Value, larvaVictim);
            }

            _container.Remove(uid, container);
            _container.Insert(larva, container);

            QueueDel(uid);
        }
    }
}
