using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
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
using Content.Shared.Mobs.Systems;
using Content.Shared.Bed.Components;
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
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetabolizerComponent, ComponentStartup>(OnMetabolizerInit);
        SubscribeLocalEvent<CirculatoryStreamComponent, AfterAutoHandleStateEvent>(OnStreamState);
        SubscribeLocalEvent<CirculatoryStreamComponent, ComponentShutdown>(OnStreamShutdown);
        SubscribeLocalEvent<CirculatoryStreamComponent, MetabolismExclusionEvent>(OnMetabolismExclusion);
        SubscribeLocalEvent<CirculatoryStreamComponent, SolutionRelayEvent<ReactionAttemptEvent>>(OnReactionAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<CirculatoryStreamComponent, BloodstreamComponent, WoundHostComponent>();
        while (query.MoveNext(out var body, out var streams, out var bloodstream, out _))
        {
            if (_timing.CurTime < streams.NextUpdate)
                continue;

            streams.NextUpdate += bloodstream.AdjustedUpdateInterval;
            Dirty(body, streams);
            foreach (var (stream, rate) in streams.BleedRates)
            {
                if (stream == CirculatoryStreamPrototype.PrimaryStream || rate <= 0f ||
                    !_prototypes.TryIndex(stream, out var prototype) ||
                    !_solutions.TryGetSolution(body, prototype.SolutionName, out var solution, out _))
                    continue;

                var ev = new BleedModifierEvent(rate, bloodstream.BleedReductionAmount);
                RaiseLocalEvent(body, ref ev);
                var bleed = ev.BleedAmount;
                if (HasComp<StasisBedBuckledComponent>(body))
                    bleed *= 0.5f;

                if (_mobState.IsDead(body))
                {
                    var remaining = solution.Value.Comp.Solution.Volume - prototype.ReferenceSolution.Volume * 0.65f;
                    if (remaining <= FixedPoint2.Zero)
                        continue;

                    bleed = Math.Min(bleed * 0.5f, remaining.Float());
                }

                if (bleed <= 0f)
                    continue;

                if (!_solutions.TryGetSolution(body, prototype.TemporarySolutionName, out var temporary, out var contents))
                    continue;

                var leaked = _solutions.SplitSolution(solution.Value, FixedPoint2.New(bleed));
                contents.AddSolution(leaked, _prototypes);
                if (contents.Volume > bloodstream.BleedPuddleThreshold)
                {
                    _puddles.TrySpillAt(body, contents, out _, sound: false);
                    contents.RemoveAllSolution();
                }

                _solutions.UpdateChemicals(temporary.Value);
            }
        }
    }

    private void OnMetabolizerInit(Entity<MetabolizerComponent> entity, ref ComponentStartup args)
    {
        if (_net.IsServer)
            return;

        if (TryComp(entity, out CirculatoryStreamComponent? streams))
            ConfigureMetabolizer(entity, streams.InitializedStreams);
    }

    private void OnStreamState(Entity<CirculatoryStreamComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp(entity, out MetabolizerComponent? metabolizer))
            ConfigureMetabolizer((entity, metabolizer), entity.Comp.InitializedStreams);
    }

    private void OnStreamShutdown(Entity<CirculatoryStreamComponent> entity, ref ComponentShutdown args)
    {
        if (TryComp(entity, out MetabolizerComponent? metabolizer))
            ConfigureMetabolizer((entity, metabolizer), new HashSet<ProtoId<CirculatoryStreamPrototype>>());
    }

    public void SynchronizeStreams(EntityUid body, EntityUid? insertedPart = null)
    {
        var attached = GetAttachedStreams(body);
        if (insertedPart is { } part && TryComp(part, out WoundableComponent? woundable))
            attached.Add(GetPartStream((part, woundable)));
        attached.Remove(CirculatoryStreamPrototype.PrimaryStream);
        if (!_net.IsServer)
        {
            if (TryComp(body, out MetabolizerComponent? clientMetabolizer) &&
                TryComp(body, out CirculatoryStreamComponent? clientStreams))
                ConfigureMetabolizer((body, clientMetabolizer), clientStreams.InitializedStreams);
            return;
        }

        if (!TryComp(body, out BloodstreamComponent? bloodstream))
            return;

        var existed = TryComp(body, out CirculatoryStreamComponent? streams);
        if (attached.Count == 0 && !existed)
            return;

        streams ??= EnsureComp<CirculatoryStreamComponent>(body);
        if (!existed)
            streams.NextUpdate = _timing.CurTime + bloodstream.AdjustedUpdateInterval;

        SynchronizeStreams(body, streams, bloodstream, attached);
        if (attached.Count == 0)
            RemComp<CirculatoryStreamComponent>(body);
    }

    private void SynchronizeStreams(EntityUid body,
        CirculatoryStreamComponent streams,
        BloodstreamComponent bloodstream,
        HashSet<ProtoId<CirculatoryStreamPrototype>>? attached = null)
    {
        attached ??= GetAttachedStreams(body);
        attached.Remove(CirculatoryStreamPrototype.PrimaryStream);

        MetabolizerComponent? metabolizer = null;
        if (attached.Count > 0)
        {
            if (!TryComp(body, out metabolizer))
            {
                metabolizer = EnsureComp<MetabolizerComponent>(body);
                streams.AddedMetabolizer = true;
            }

            if (streams.InitializedStreams.Count == 0)
                streams.PreviousMaxReagentsProcessable = metabolizer.MaxReagentsProcessable;
        }

        foreach (var stream in attached)
        {
            if (metabolizer != null)
                InitializeStream(body, streams, bloodstream, metabolizer, stream);
        }

        foreach (var stream in new List<ProtoId<CirculatoryStreamPrototype>>(streams.InitializedStreams))
        {
            if (!attached.Contains(stream))
                RemoveStream(body, streams, stream);
        }

        if (metabolizer != null)
            ConfigureMetabolizer((body, metabolizer), streams.InitializedStreams);

        if (attached.Count == 0 && TryComp(body, out metabolizer))
        {
            ConfigureMetabolizer((body, metabolizer), attached);
            if (streams.AddedMetabolizer)
                RemComp<MetabolizerComponent>(body);
            else
                metabolizer.MaxReagentsProcessable = streams.PreviousMaxReagentsProcessable;
            streams.AddedMetabolizer = false;
        }

        Dirty(body, streams);
    }

    private void ConfigureMetabolizer(Entity<MetabolizerComponent> metabolizer,
        HashSet<ProtoId<CirculatoryStreamPrototype>> active)
    {
        if (!TryComp(metabolizer.Owner, out CirculatoryStreamComponent? streams))
            return;

        var maxReagents = streams.PreviousMaxReagentsProcessable;
        foreach (var prototype in _prototypes.EnumeratePrototypes<CirculatoryStreamPrototype>())
        {
            if (prototype.ID == CirculatoryStreamPrototype.PrimaryStream)
                continue;

            if (!active.Contains(prototype.ID))
            {
                if (!streams.ConfiguredStreams.Contains(prototype.ID))
                    continue;

                if (metabolizer.Comp.Solutions.TryGetValue(prototype.MetabolismStage, out var metabolism) &&
                    metabolism.SolutionName == prototype.SolutionName)
                    metabolizer.Comp.Solutions.Remove(prototype.MetabolismStage);
                if (metabolizer.Comp.Solutions.TryGetValue(prototype.MetabolitesStage, out var metabolites) &&
                    metabolites.SolutionName == prototype.MetabolitesSolutionName)
                    metabolizer.Comp.Solutions.Remove(prototype.MetabolitesStage);
                metabolizer.Comp.Stages.Remove(prototype.MetabolismStage);
                metabolizer.Comp.Stages.Remove(prototype.MetabolitesStage);
                continue;
            }

            maxReagents = Math.Max(maxReagents, prototype.MaxReagentsProcessable);
            metabolizer.Comp.Solutions[prototype.MetabolismStage] = new MetabolismSolutionEntry
            {
                SolutionName = prototype.SolutionName,
                SolutionOnBody = false,
                TransferSolutionName = prototype.MetabolitesSolutionName,
                TransferSolutionOnBody = false,
                TransferRate = prototype.MetabolismTransferRate,
            };
            metabolizer.Comp.Solutions[prototype.MetabolitesStage] = new MetabolismSolutionEntry
            {
                SolutionName = prototype.MetabolitesSolutionName,
                SolutionOnBody = false,
            };
            metabolizer.Comp.Stages.Add(prototype.MetabolismStage);
            metabolizer.Comp.Stages.Add(prototype.MetabolitesStage);
        }

        streams.ConfiguredStreams.Clear();
        streams.ConfiguredStreams.UnionWith(active);
        metabolizer.Comp.MaxReagentsProcessable = maxReagents;
        Dirty(metabolizer);
    }

    private void InitializeStream(EntityUid body,
        CirculatoryStreamComponent streams,
        BloodstreamComponent bloodstream,
        MetabolizerComponent metabolizer,
        ProtoId<CirculatoryStreamPrototype> stream)
    {
        if (streams.InitializedStreams.Contains(stream) || !_prototypes.TryIndex(stream, out var prototype))
            return;

        if (metabolizer.Solutions.ContainsKey(prototype.MetabolismStage) ||
            metabolizer.Solutions.ContainsKey(prototype.MetabolitesStage) ||
            HasStageConflict(streams.InitializedStreams, prototype))
            return;

        if (!_solutions.TryCreateCirculatorySolution(body, prototype.SolutionName, out var solution))
            return;

        if (!_solutions.TryCreateCirculatorySolution(body, prototype.MetabolitesSolutionName, out var metabolites))
        {
            DeleteSolution(body, prototype.SolutionName);
            return;
        }

        if (!_solutions.TryCreateCirculatorySolution(body, prototype.TemporarySolutionName, out var temporary))
        {
            DeleteSolution(body, prototype.SolutionName);
            DeleteSolution(body, prototype.MetabolitesSolutionName);
            return;
        }

        _solutions.SetCapacity(solution, prototype.ReferenceSolution.Volume * prototype.MaxVolumeModifier);
        _solutions.SetCapacity(metabolites, solution.Comp.Solution.MaxVolume);
        _solutions.SetCapacity(temporary, bloodstream.BleedPuddleThreshold * 4);

        var fill = prototype.ReferenceSolution.Clone();
        fill.ScaleTo(prototype.ReferenceSolution.Volume);
        _solutions.TryAddSolution(solution, fill);
        streams.InitializedStreams.Add(stream);
    }

    private bool HasStageConflict(HashSet<ProtoId<CirculatoryStreamPrototype>> active,
        CirculatoryStreamPrototype candidate)
    {
        foreach (var stream in active)
        {
            if (!_prototypes.TryIndex(stream, out var prototype))
                continue;

            if (prototype.MetabolismStage == candidate.MetabolismStage ||
                prototype.MetabolismStage == candidate.MetabolitesStage ||
                prototype.MetabolitesStage == candidate.MetabolismStage ||
                prototype.MetabolitesStage == candidate.MetabolitesStage)
                return true;
        }

        return false;
    }

    private void RemoveStream(EntityUid body,
        CirculatoryStreamComponent streams,
        ProtoId<CirculatoryStreamPrototype> stream)
    {
        if (!_prototypes.TryIndex(stream, out var prototype))
            return;

        DeleteSolution(body, prototype.SolutionName);
        DeleteSolution(body, prototype.MetabolitesSolutionName);
        DeleteSolution(body, prototype.TemporarySolutionName);
        streams.InitializedStreams.Remove(stream);
        streams.BleedRates.Remove(stream);
    }

    private void DeleteSolution(EntityUid body, string name)
    {
        _solutions.TryDeleteCirculatorySolution(body, name);
    }

    private HashSet<ProtoId<CirculatoryStreamPrototype>> GetAttachedStreams(EntityUid body)
    {
        var streams = new HashSet<ProtoId<CirculatoryStreamPrototype>>();
        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            if (TryComp(part, out WoundableComponent? woundable))
                streams.Add(GetPartStream((part, woundable)));
        }

        return streams;
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
            : CirculatoryStreamPrototype.PrimaryStream;
    }

    public bool TryGetPartSolution(EntityUid body, EntityUid part, out Entity<SolutionComponent> solution)
    {
        solution = default;
        if (!_body.BodyHasChild(body, part) || !TryComp(part, out WoundableComponent? woundable))
            return false;

        var stream = GetPartStream((part, woundable));
        if (stream == CirculatoryStreamPrototype.PrimaryStream && TryComp(body, out BloodstreamComponent? bloodstream))
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

    public bool TryGetStreamSolution(EntityUid body,
        ProtoId<CirculatoryStreamPrototype> stream,
        out Entity<SolutionComponent> solution)
    {
        solution = default;
        if (stream == CirculatoryStreamPrototype.PrimaryStream && TryComp(body, out BloodstreamComponent? bloodstream))
        {
            if (!_solutions.ResolveSolution(body, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out _))
                return false;

            solution = bloodstream.BloodSolution.Value;
            return true;
        }

        if (!TryComp(body, out CirculatoryStreamComponent? streams) ||
            !streams.InitializedStreams.Contains(stream) ||
            !_prototypes.TryIndex(stream, out var prototype) ||
            !_solutions.TryGetSolution(body, prototype.SolutionName, out Entity<SolutionComponent>? found, out _))
            return false;

        solution = found.Value;
        return true;
    }

    public void SetBleedRates(EntityUid body, Dictionary<ProtoId<CirculatoryStreamPrototype>, float> rates)
    {
        if (!TryComp(body, out BloodstreamComponent? bloodstream))
            return;

        _bloodstream.TryModifyWoundBleedProjection((body, bloodstream),
            rates.GetValueOrDefault(CirculatoryStreamPrototype.PrimaryStream) - bloodstream.BleedAmount);

        if (TryComp(body, out CirculatoryStreamComponent? streams))
        {
            streams.BleedRates = rates;
            Dirty(body, streams);
            return;
        }

        var hasSecondaryStream = false;
        foreach (var stream in rates.Keys)
        {
            if (stream == CirculatoryStreamPrototype.PrimaryStream)
                continue;

            hasSecondaryStream = true;
            break;
        }

        if (!hasSecondaryStream)
            return;

        SynchronizeStreams(body);
        if (!TryComp(body, out streams))
            return;

        streams.BleedRates = rates;
        Dirty(body, streams);
    }
}
