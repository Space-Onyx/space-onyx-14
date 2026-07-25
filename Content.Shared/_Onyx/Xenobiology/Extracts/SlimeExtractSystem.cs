using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;
using Content.Shared.Tag;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Xenobiology.Extracts;

public sealed partial class SlimeExtractSystem : EntityEffectSystem<SlimeExtractComponent, UseSlimeExtract>
{
    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeExtractComponent, ExaminedEvent>(OnExamined);
    }

    protected override void Effect(Entity<SlimeExtractComponent> entity, ref EntityEffectEvent<UseSlimeExtract> args)
    {
        if (entity.Comp.Used || entity.Comp.Processing)
            return;

        Entity<SolutionComponent>? solutionEntity = null;
        Solution? original = null;
        if (_solutions.TryGetRefillableSolution(entity.Owner, out var solution, out var contents))
        {
            solutionEntity = solution.Value;
            original = contents.Clone();
            _solutions.RemoveAllSolution(solution.Value);
        }

        entity.Comp.Processing = true;
        try
        {
            _effects.ApplyEffects(entity, args.Effect.Effects, 1f, args.User);
        }
        catch
        {
            if (solutionEntity is { } target && original is { } restore)
            {
                _solutions.RemoveAllSolution(target);
                _solutions.TryAddSolution(target, restore);
            }
            throw;
        }
        finally
        {
            entity.Comp.Processing = false;
        }

        if (Deleted(entity))
            return;

        entity.Comp.Used = true;
        RemComp<ReactiveComponent>(entity);

        _tags.AddTag(entity, TrashTag);
        _appearance.SetData(entity, SlimeExtractVisuals.Used, true);
        Dirty(entity);
    }

    private void OnExamined(Entity<SlimeExtractComponent> entity, ref ExaminedEvent args)
    {
        if (entity.Comp.Used)
            args.PushText(Loc.GetString("xenobio-slime-extract-examine-used"));
    }
}

public sealed partial class AdjustExtractReagentSystem : EntityEffectSystem<SlimeExtractComponent, AdjustExtractReagent>
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    protected override void Effect(Entity<SlimeExtractComponent> entity, ref EntityEffectEvent<AdjustExtractReagent> args)
    {
        if (!_solutions.TryGetRefillableSolution(entity.Owner, out var solution, out _))
            return;

        var quantity = args.Effect.Amount * args.Scale;
        if (quantity > 0)
            _solutions.TryAddReagent(solution.Value, args.Effect.Reagent, quantity, out _);
        else
            _solutions.RemoveReagent(solution.Value, args.Effect.Reagent, -quantity);
    }
}

public sealed partial class ModifySlimeSystem : EntityEffectSystem<XenobioSlimeComponent, ModifySlime>
{
    protected override void Effect(Entity<XenobioSlimeComponent> entity, ref EntityEffectEvent<ModifySlime> args)
    {
        var effect = args.Effect;
        entity.Comp.ExtractsProduced = Math.Max(1, entity.Comp.ExtractsProduced + effect.ExtractBonus);
        entity.Comp.MaxOffspring = Math.Max(entity.Comp.MinOffspring, entity.Comp.MaxOffspring + effect.OffspringBonus);
        entity.Comp.MutationChance = Math.Clamp(entity.Comp.MutationChance + effect.ChanceModifier, 0f, 1f);
        Dirty(entity);
    }
}
