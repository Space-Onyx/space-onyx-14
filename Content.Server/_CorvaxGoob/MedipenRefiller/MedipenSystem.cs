using Content.Shared._CorvaxGoob.MedipenRefiller;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxGoob.MedipenRefiller;

public sealed partial class MedipenSystem : EntitySystem
{
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedipenComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MedipenComponent, InjectorDoAfterEvent>(OnInjectorDoAfter, after: [typeof(InjectorSystem)]);
    }

    public void UpdateAppearance(Entity<MedipenComponent> entity, Entity<SolutionComponent> solution)
    {
        var filled = solution.Comp.Solution.Volume > 0;
        entity.Comp.Used = !filled;
        _appearance.SetData(entity, MedipenVisualLayer.Fill, filled);

        if (filled)
            _tag.RemoveTag(entity.Owner, TrashTag);
        else if (entity.Comp.TrashOnUse)
            _tag.AddTag(entity.Owner, TrashTag);

        Dirty(entity);
    }

    private void OnMapInit(Entity<MedipenComponent> entity, ref MapInitEvent args)
    {
        _appearance.SetData(entity, MedipenVisualLayer.Fill, true);
    }

    private void OnInjectorDoAfter(Entity<MedipenComponent> entity, ref InjectorDoAfterEvent args)
    {
        if (args.Cancelled ||
            !_solutionContainer.TryGetSolution(entity.Owner, MedipenRefillerSystem.MedipenSolutionName, out var solution, out _))
            return;

        UpdateAppearance(entity, solution.Value);
    }
}
