using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Body;
using Robust.Shared.GameObjects;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.EntityEffects.Effects.EntitySpawning;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Metabolism;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Chemistry.Circulation;

public sealed partial class CirculatoryStreamSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedPuddleSystem _puddles = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundHostComponent, ComponentStartup>(OnHostInit);
        SubscribeLocalEvent<CirculatoryStreamComponent, MetabolismExclusionEvent>(OnMetabolismExclusion);
        SubscribeLocalEvent<CirculatoryStreamComponent, SolutionRelayEvent<ReactionAttemptEvent>>(OnReactionAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<CirculatoryStreamComponent, BloodstreamComponent, WoundHostComponent>();
        while (query.MoveNext(out var body, out var streams, out var bloodstream, out var host))
        {
            if (_timing.CurTime < streams.NextUpdate)
                continue;

            streams.NextUpdate += bloodstream.AdjustedUpdateInterval;
            Dirty(body, streams);
            foreach (var (stream, rate) in streams.BleedRates)
            {
                if (stream == host.PrimaryCirculatoryStream || rate <= 0f ||
                    !_prototypes.TryIndex(stream, out var prototype) ||
                    !_solutions.TryGetSolution(body, prototype.SolutionName, out var solution, out _))
                    continue;

                var leaked = _solutions.SplitSolution(solution.Value, FixedPoint2.New(rate));
                if (leaked.Volume > FixedPoint2.Zero)
                    _puddles.TrySpillAt(body, leaked, out _, sound: false);
            }
        }
    }

    private void OnHostInit(Entity<WoundHostComponent> body, ref ComponentStartup args)
    {
        if (!_net.IsServer || !TryComp(body, out BloodstreamComponent? bloodstream))
            return;

        var streams = EnsureComp<CirculatoryStreamComponent>(body);
        streams.NextUpdate = _timing.CurTime + bloodstream.AdjustedUpdateInterval;
        var metabolizer = EnsureComp<MetabolizerComponent>(body);
        metabolizer.SolutionOnBody = false;

        foreach (var prototype in _prototypes.EnumeratePrototypes<CirculatoryStreamPrototype>())
        {
            if (prototype.ID == body.Comp.PrimaryCirculatoryStream)
                continue;

            _solutions.EnsureSolution(body.Owner, prototype.SolutionName, out var solution);
            _solutions.EnsureSolution(body.Owner, prototype.MetabolitesSolutionName, out var metabolites);
            _solutions.SetCapacity(solution, prototype.ReferenceSolution.Volume * prototype.MaxVolumeModifier);
            _solutions.SetCapacity(metabolites, solution.Comp.Solution.MaxVolume);
            metabolizer.MaxReagentsProcessable = Math.Max(metabolizer.MaxReagentsProcessable,
                prototype.MaxReagentsProcessable);
            metabolizer.Solutions[prototype.MetabolismStage] = new MetabolismSolutionEntry
            {
                SolutionName = prototype.SolutionName,
                SolutionOnBody = false,
                TransferSolutionName = prototype.MetabolitesSolutionName,
                TransferSolutionOnBody = false,
                TransferRate = prototype.MetabolismTransferRate,
            };
            metabolizer.Solutions[prototype.MetabolitesStage] = new MetabolismSolutionEntry
            {
                SolutionName = prototype.MetabolitesSolutionName,
                SolutionOnBody = false,
            };
            if (!metabolizer.Stages.Contains(prototype.MetabolismStage))
                metabolizer.Stages.Add(prototype.MetabolismStage);
            if (!metabolizer.Stages.Contains(prototype.MetabolitesStage))
                metabolizer.Stages.Add(prototype.MetabolitesStage);
        }

        foreach (var prototype in _prototypes.EnumeratePrototypes<CirculatoryStreamPrototype>())
        {
            if (prototype.ID == body.Comp.PrimaryCirculatoryStream)
                continue;

            InitializeStream(body, streams, prototype.ID);
        }

        Dirty(body, streams);
        Dirty(body, metabolizer);
    }

    private void InitializeStream(EntityUid body,
        CirculatoryStreamComponent streams,
        ProtoId<CirculatoryStreamPrototype> stream)
    {
        if (!streams.InitializedStreams.Add(stream) || !TryComp(body, out WoundHostComponent? host) ||
            stream == host.PrimaryCirculatoryStream || !_prototypes.TryIndex(stream, out var prototype) ||
            !_solutions.TryGetSolution(body, prototype.SolutionName, out var solution, out var contents))
            return;

        var fill = prototype.ReferenceSolution.Clone();
        fill.ScaleTo(FixedPoint2.Max(FixedPoint2.Zero, prototype.ReferenceSolution.Volume - contents.Volume));
        _solutions.TryAddSolution(solution.Value, fill);
        Dirty(body, streams);
    }

    private void OnMetabolismExclusion(Entity<CirculatoryStreamComponent> body,
        ref MetabolismExclusionEvent args)
    {
        if (args.SolutionName == null)
            return;

        foreach (var prototype in _prototypes.EnumeratePrototypes<CirculatoryStreamPrototype>())
        {
            if (prototype.SolutionName != args.SolutionName)
                continue;

            foreach (var (reagent, _) in prototype.ReferenceSolution)
                args.Reagents.Add(reagent);
            return;
        }
    }

    private void OnReactionAttempt(Entity<CirculatoryStreamComponent> body,
        ref SolutionRelayEvent<ReactionAttemptEvent> args)
    {
        var circulatorySolution = false;
        foreach (var stream in _prototypes.EnumeratePrototypes<CirculatoryStreamPrototype>())
        {
            if (stream.SolutionName != args.Solution.Comp.Id)
                continue;

            circulatorySolution = true;
            break;
        }

        if (!circulatorySolution)
            return;

        foreach (var effect in args.Event.Reaction.Effects)
        {
            if (effect is SpawnEntity or AreaReactionEffect)
            {
                args.Event.Cancelled = true;
                return;
            }
        }
    }

    public ProtoId<CirculatoryStreamPrototype> GetPartStream(Entity<WoundableComponent> part)
    {
        return _prototypes.TryIndex(part.Comp.Profile, out var profile)
            ? profile.CirculatoryStream
            : "Organic";
    }

    public bool TryGetPartSolution(EntityUid body, EntityUid part, out Entity<SolutionComponent> solution)
    {
        solution = default;
        if (!_body.BodyHasChild(body, part) || !TryComp(part, out WoundableComponent? woundable) ||
            !TryComp(body, out WoundHostComponent? host))
            return false;

        var stream = GetPartStream((part, woundable));
        if (stream == host.PrimaryCirculatoryStream && TryComp(body, out BloodstreamComponent? bloodstream))
        {
            if (!_solutions.ResolveSolution(body, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out _))
                return false;

            solution = bloodstream.BloodSolution.Value;
            return true;
        }

        if (!_prototypes.TryIndex(stream, out var prototype) ||
            !_solutions.TryGetSolution(body, prototype.SolutionName, out var found, out _))
            return false;

        solution = found.Value;
        return true;
    }

    public void SetBleedRates(EntityUid body, Dictionary<ProtoId<CirculatoryStreamPrototype>, float> rates)
    {
        if (!TryComp(body, out WoundHostComponent? host) || !TryComp(body, out CirculatoryStreamComponent? streams) ||
            !TryComp(body, out BloodstreamComponent? bloodstream))
            return;

        streams.BleedRates = rates;
        Dirty(body, streams);
        _bloodstream.TryModifyWoundBleedProjection((body, bloodstream),
            rates.GetValueOrDefault(host.PrimaryCirculatoryStream) - bloodstream.BleedAmount);
    }

    private HashSet<ProtoId<CirculatoryStreamPrototype>> GetAttachedStreams(EntityUid body)
    {
        var result = new HashSet<ProtoId<CirculatoryStreamPrototype>>();
        foreach (var (part, _) in _body.GetBodyChildren(body))
            if (TryComp(part, out WoundableComponent? woundable))
                result.Add(GetPartStream((part, woundable)));
        return result;
    }
}
