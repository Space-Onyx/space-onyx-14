using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Mobs.Growth;

public sealed partial class MobGrowthSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NameModifierSystem _nameModifier = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobGrowthComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MobGrowthComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
    }

    private void OnRefreshNameModifiers(Entity<MobGrowthComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (ent.Comp.Stages.TryGetValue(ent.Comp.CurrentStage, out var stage) && stage.NamePrefix is { } prefix)
            args.AddModifier("mob-growth-stage-name", extraArgs: ("stage", Loc.GetString(prefix)));
    }

    private void OnMapInit(Entity<MobGrowthComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || !Validate(ent))
            return;

        if (string.IsNullOrEmpty(ent.Comp.CurrentStage))
        {
            ent.Comp.CurrentStage = ent.Comp.InitialStage;
            DirtyField(ent.Owner, ent.Comp, nameof(MobGrowthComponent.CurrentStage));
        }

        ent.Comp.NextGrowth = _timing.CurTime + ent.Comp.GrowthInterval;
        ApplyStage(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<MobGrowthComponent, SatiationComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var growth, out var satiation, out var mobState))
        {
            if (_timing.CurTime < growth.NextGrowth)
                continue;

            growth.NextGrowth = _timing.CurTime + growth.GrowthInterval;

            if (!_mobState.IsAlive(uid, mobState))
                continue;

            TryGrow((uid, growth), satiation);
        }
    }

    public bool TryGrow(Entity<MobGrowthComponent> ent, SatiationComponent? satiation = null)
    {
        if (_net.IsClient ||
            TerminatingOrDeleted(ent) ||
            !TryComp<MobStateComponent>(ent, out var mobState) ||
            !_mobState.IsAlive(ent.Owner, mobState) ||
            !Resolve(ent.Owner, ref satiation, false))
            return false;

        var hungerValue = _satiation.GetValueOrNull((ent.Owner, satiation), SatiationSystem.Hunger);
        if (hungerValue == null ||
            hungerValue.Value < ent.Comp.HungerRequired ||
            !ent.Comp.Stages.TryGetValue(ent.Comp.CurrentStage, out var current) ||
            current.NextStage is not { } nextStage ||
            !ent.Comp.Stages.ContainsKey(nextStage))
        {
            return false;
        }

        var oldStage = ent.Comp.CurrentStage;
        _satiation.ModifyValue((ent.Owner, satiation), SatiationSystem.Hunger, -ent.Comp.HungerCost);
        ent.Comp.CurrentStage = nextStage;
        DirtyField(ent.Owner, ent.Comp, nameof(MobGrowthComponent.CurrentStage));
        ApplyStage(ent);

        var ev = new MobGrowthStageChangedEvent(oldStage, nextStage);
        RaiseLocalEvent(ent.Owner, ref ev);
        return true;
    }

    public bool IsInitialStage(Entity<MobGrowthComponent> ent)
    {
        return ent.Comp.CurrentStage == ent.Comp.InitialStage;
    }

    private bool Validate(Entity<MobGrowthComponent> ent)
    {
        var growth = ent.Comp;
        var valid = true;

        if (growth.GrowthInterval <= TimeSpan.Zero)
        {
            Log.Error($"MobGrowth on {ToPrettyString(ent)} has a non-positive growth interval.");
            valid = false;
        }

        if (!float.IsFinite(growth.HungerRequired) ||
            !float.IsFinite(growth.HungerCost) ||
            growth.HungerRequired < 0f ||
            growth.HungerCost < 0f)
        {
            Log.Error($"MobGrowth on {ToPrettyString(ent)} has invalid hunger values.");
            valid = false;
        }

        if (!growth.Stages.ContainsKey(growth.InitialStage))
        {
            Log.Error($"MobGrowth on {ToPrettyString(ent)} has unknown initial stage '{growth.InitialStage}'.");
            valid = false;
        }

        if (!string.IsNullOrEmpty(growth.CurrentStage) && !growth.Stages.ContainsKey(growth.CurrentStage))
        {
            Log.Error($"MobGrowth on {ToPrettyString(ent)} has unknown current stage '{growth.CurrentStage}'.");
            valid = false;
        }

        foreach (var (id, stage) in growth.Stages)
        {
            if (stage.NextStage is { } next && !growth.Stages.ContainsKey(next))
            {
                Log.Error($"MobGrowth stage '{id}' on {ToPrettyString(ent)} points to unknown stage '{next}'.");
                valid = false;
            }
        }

        if (HasCycle(growth))
        {
            Log.Error($"MobGrowth on {ToPrettyString(ent)} contains a stage cycle.");
            valid = false;
        }

        return valid;
    }

    private static bool HasCycle(MobGrowthComponent growth)
    {
        var visited = new HashSet<string>();
        var active = new HashSet<string>();

        foreach (var stage in growth.Stages.Keys)
        {
            if (Visit(stage))
                return true;
        }

        return false;

        bool Visit(string stage)
        {
            if (active.Contains(stage))
                return true;

            if (!visited.Add(stage))
                return false;

            active.Add(stage);
            if (growth.Stages.TryGetValue(stage, out var data) &&
                data.NextStage is { } next &&
                Visit(next))
                return true;

            active.Remove(stage);
            return false;
        }
    }

    private void ApplyStage(Entity<MobGrowthComponent> ent)
    {
        if (!ent.Comp.Stages.TryGetValue(ent.Comp.CurrentStage, out var stage))
            return;

        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.SetData(ent.Owner, MobGrowthVisuals.Stage, ent.Comp.CurrentStage, appearance);

        _nameModifier.RefreshNameModifiers(ent.Owner);

        if (stage.Description is { } description)
            _metaData.SetEntityDescription(ent.Owner, Loc.GetString(description));
    }
}
