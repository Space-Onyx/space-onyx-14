using Content.Shared._Onyx.Mobs.Growth;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Xenobiology.Slimes;

public sealed partial class SlimeBreedingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SlimeLatchSystem _latch = default!;
    [Dependency] private IGameTiming _timing = default!;

    private bool _breedingEnabled;
    private TimeSpan _breedingInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobioSlimeComponent, InteractionSuccessEvent>(OnInteractionSuccess);
        Subs.CVar(_configuration, CCVars.XenobiologyBreedingEnabled, value => _breedingEnabled = value, true);
        Subs.CVar(_configuration, CCVars.XenobiologyBreedingInterval, OnBreedingIntervalChanged, true);
    }

    private void OnBreedingIntervalChanged(float value)
    {
        _breedingInterval = TimeSpan.FromSeconds(Math.Max(0.05f, value));

        var query = EntityQueryEnumerator<XenobioSlimeComponent>();
        while (query.MoveNext(out var uid, out var slime))
        {
            slime.MitosisInterval = _breedingInterval;
            slime.NextMitosis = _timing.CurTime + _breedingInterval;
            Dirty(uid, slime);
        }
    }

    public void InitializeSlime(Entity<XenobioSlimeComponent> slime)
    {
        slime.Comp.MitosisInterval = _breedingInterval;
        slime.Comp.NextMitosis = _timing.CurTime + _breedingInterval;
        if (!RemComp<RandomizeXenobioSlimeComponent>(slime))
            return;

        slime.Comp.MutationChance = Math.Clamp(slime.Comp.MutationChance * _random.NextFloat(0.5f, 1.5f), 0f, 1f);
        slime.Comp.MaxOffspring = Math.Max(slime.Comp.MinOffspring, slime.Comp.MaxOffspring + _random.Next(-1, 2));
        slime.Comp.ExtractsProduced = Math.Max(1, slime.Comp.ExtractsProduced + _random.Next(0, 2));
        slime.Comp.MitosisHunger = Math.Max(0f, slime.Comp.MitosisHunger * _random.NextFloat(0.75f, 1.2f));
        Dirty(slime);
    }

    private void OnInteractionSuccess(Entity<XenobioSlimeComponent> slime, ref InteractionSuccessEvent args)
    {
        if (slime.Comp.Tamer != null)
        {
            _popup.PopupEntity(Loc.GetString("xenobio-slime-tame-fail"), args.User, args.User);
            return;
        }

        Spawn(slime.Comp.TameEffect, Transform(slime).Coordinates);
        slime.Comp.Tamer = args.User;
        Dirty(slime);
        _popup.PopupEntity(Loc.GetString("xenobio-slime-tame-success"), args.User, args.User);
    }

    public override void Update(float frameTime)
    {
        if (!_breedingEnabled)
            return;

        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<XenobioSlimeComponent, MobGrowthComponent, SatiationComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var slime, out var growth, out var satiation, out var mobState))
        {
            if (_timing.CurTime < slime.NextMitosis)
                continue;

            slime.NextMitosis = _timing.CurTime + slime.MitosisInterval;
            if (!_mobState.IsAlive(uid, mobState) ||
                growth.CurrentStage == growth.InitialStage ||
                (_satiation.GetValueOrNull((uid, satiation), SatiationSystem.Hunger) ?? 0f) < slime.MitosisHunger)
                continue;

            candidates.Add(uid);
        }

        foreach (var uid in candidates)
        {
            if (TryComp<XenobioSlimeComponent>(uid, out var slime))
                TryMitosis((uid, slime));
        }
    }

    public bool TryMitosis(Entity<XenobioSlimeComponent> parent, int? forcedCount = null)
    {
        if (TerminatingOrDeleted(parent) ||
            parent.Comp.MinOffspring <= 0 ||
            parent.Comp.MaxOffspring < parent.Comp.MinOffspring)
            return false;

        var count = forcedCount ?? _random.Next(parent.Comp.MinOffspring, parent.Comp.MaxOffspring + 1);
        if (count < parent.Comp.MinOffspring || count > parent.Comp.MaxOffspring)
            return false;

        var children = new List<Entity<XenobioSlimeComponent>>(count);
        for (var i = 0; i < count; i++)
        {
            var breed = SelectBreed(parent.Comp);
            if (!TrySpawnNextTo(breed, parent, out var childUid) ||
                childUid is not { } child ||
                !TryComp<XenobioSlimeComponent>(child, out var childComp))
            {
                if (childUid is { } invalid)
                    QueueDel(invalid);
                continue;
            }

            childComp.Tamer = parent.Comp.Tamer;
            childComp.MutationChance = parent.Comp.MutationChance;
            childComp.MinOffspring = parent.Comp.MinOffspring;
            childComp.MaxOffspring = parent.Comp.MaxOffspring;
            childComp.ExtractsProduced = parent.Comp.ExtractsProduced;
            childComp.MitosisHunger = parent.Comp.MitosisHunger;
            children.Add((child, childComp));
            Dirty(child, childComp);
        }

        if (children.Count == 0)
            return false;

        DistributeStomachs(parent, children);
        DistributeBloodstreamChemicals(parent, children);
        _latch.Unlatch(parent);
        _audio.PlayPvs(parent.Comp.MitosisSound, parent);
        QueueDel(parent);
        return true;
    }

    private EntProtoId SelectBreed(XenobioSlimeComponent parent)
    {
        if (!_random.Prob(parent.MutationChance) || parent.PotentialMutations.Count == 0)
            return parent.Breed;

        var valid = new List<EntProtoId>();
        foreach (var mutation in parent.PotentialMutations)
        {
            if (_prototypes.TryIndex(mutation, out var prototype) &&
                prototype.TryComp<XenobioSlimeComponent>(out _, EntityManager.ComponentFactory))
                valid.Add(mutation);
        }
        return valid.Count == 0 ? parent.Breed : _random.Pick(valid);
    }

    private void DistributeStomachs(EntityUid parent, List<Entity<XenobioSlimeComponent>> children)
    {
        var source = new Solution();
        foreach (var (organ, _) in _body.GetBodyOrgans(parent))
        {
            if (!TryComp<StomachComponent>(organ, out var stomach) ||
                !_solutions.ResolveSolution(organ, StomachSystem.DefaultSolutionName, ref stomach.Solution, out var solution))
                continue;
            source.AddSolution(solution, _prototypes);
            _solutions.RemoveAllSolution(stomach.Solution!.Value);
        }
        Distribute(source, children, GetStomachSolutions);
    }

    private void DistributeBloodstreamChemicals(EntityUid parent, List<Entity<XenobioSlimeComponent>> children)
    {
        if (!TryComp<BloodstreamComponent>(parent, out var bloodstream))
            return;

        DistributeBloodstreamSolution(parent,
            bloodstream.MetabolitesSolutionName,
            ref bloodstream.MetabolitesSolution,
            children,
            static component => (component.MetabolitesSolutionName, component.MetabolitesSolution));
        DistributeBloodstreamSolution(parent,
            bloodstream.BloodTemporarySolutionName,
            ref bloodstream.TemporarySolution,
            children,
            static component => (component.BloodTemporarySolutionName, component.TemporarySolution));
    }

    private void DistributeBloodstreamSolution(EntityUid parent,
        string name,
        ref Entity<SolutionComponent>? cache,
        List<Entity<XenobioSlimeComponent>> children,
        Func<BloodstreamComponent, (string Name, Entity<SolutionComponent>? Cache)> selector)
    {
        if (!_solutions.ResolveSolution(parent, name, ref cache, out var solution) || solution.Volume == FixedPoint2.Zero)
            return;

        var source = solution.Clone();
        _solutions.RemoveAllSolution(cache!.Value);
        Distribute(source, children, child =>
        {
            if (!TryComp<BloodstreamComponent>(child, out var bloodstream))
                return [];
            var selected = selector(bloodstream);
            var childCache = selected.Cache;
            return _solutions.ResolveSolution(child.Owner, selected.Name, ref childCache, out _)
                ? [childCache!.Value]
                : [];
        });
    }

    private List<Entity<SolutionComponent>> GetStomachSolutions(Entity<XenobioSlimeComponent> child)
    {
        var result = new List<Entity<SolutionComponent>>();
        foreach (var (organ, _) in _body.GetBodyOrgans(child))
        {
            if (!TryComp<StomachComponent>(organ, out var stomach) ||
                !_solutions.ResolveSolution(organ, StomachSystem.DefaultSolutionName, ref stomach.Solution))
                continue;
            result.Add(stomach.Solution!.Value);
        }
        return result;
    }

    private void Distribute(Solution source,
        List<Entity<XenobioSlimeComponent>> children,
        Func<Entity<XenobioSlimeComponent>, List<Entity<SolutionComponent>>> getTargets)
    {
        for (var i = 0; i < children.Count && source.Volume > FixedPoint2.Zero; i++)
        {
            var remainingChildren = children.Count - i;
            var share = source.SplitSolution(source.Volume / FixedPoint2.New(remainingChildren));
            foreach (var target in getTargets(children[i]))
            {
                if (share.Volume == FixedPoint2.Zero)
                    break;
                _solutions.TryTransferSolution(target, share, share.Volume);
            }
        }
    }
}
