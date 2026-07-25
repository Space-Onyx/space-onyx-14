using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.PassiveConsumable;

public sealed partial class PassiveConsumableSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IngestionSystem _ingestion = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private StomachSystem _stomach = default!;
    [Dependency] private ReactiveSystem _reactive = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private FlavorProfileSystem _flavor = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PassiveConsumableComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<PassiveConsumableComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<PassiveConsumableComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        ent.Comp.NextConsume = _timing.CurTime + ent.Comp.ConsumeInterval;
        Dirty(ent);

        if (!TryComp<EdibleComponent>(ent.Owner, out var edible)
            || !_solution.TryGetSolution(ent.Owner, edible.Solution, out _, out var solution))
            return;

        var flavors = _flavor.GetLocalizedFlavorsMessage(args.Wearer, solution);
        _popup.PopupEntity(Loc.GetString("edible-nom", ("food", ent.Owner), ("flavors", flavors)), args.Wearer, args.Wearer);
    }

    private void OnUnequipped(Entity<PassiveConsumableComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
        ent.Comp.NextConsume = TimeSpan.Zero;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<PassiveConsumableComponent, ClothingComponent, EdibleComponent>();
        List<(EntityUid Uid, EdibleComponent Edible, EntityUid User)>? finished = null;

        while (query.MoveNext(out var uid, out var comp, out var clothing, out var edible))
        {
            if (clothing.InSlotFlag != comp.Slot
                || comp.NextConsume == TimeSpan.Zero
                || comp.NextConsume > _timing.CurTime)
                continue;

            if (TryConsume((uid, comp), edible) && comp.DeleteOnEmpty)
            {
                finished ??= [];
                finished.Add((uid, edible, comp.Wearer!.Value));
            }

            comp.NextConsume = _timing.CurTime + comp.ConsumeInterval;
        }

        if (finished == null)
            return;

        foreach (var (uid, edible, user) in finished)
        {
            _ingestion.SpawnTrash((uid, edible), user);
            QueueDel(uid);
        }
    }

    private bool TryConsume(Entity<PassiveConsumableComponent> ent, EdibleComponent edible)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return false;

        if (!_body.TryGetOrgansWithComponent<StomachComponent>(wearer, out var stomachs))
            return false;

        if (!_solution.TryGetSolution(ent.Owner, edible.Solution, out var solutionEntity, out var solution))
            return false;

        var transferAmount = FixedPoint2.Min(ent.Comp.Amount, solution.Volume);
        var split = _solution.SplitSolution(solutionEntity.Value, transferAmount);

        Entity<StomachComponent>? bestStomach = null;
        var highestAvailable = FixedPoint2.Zero;
        foreach (var stomach in stomachs)
        {
            if (!_solution.ResolveSolution(stomach.Owner, StomachSystem.DefaultSolutionName, ref stomach.Comp.Solution, out var stomachSolution)
                || stomachSolution.AvailableVolume <= highestAvailable
                || !_stomach.CanTransferSolution((stomach.Owner, stomach.Comp, null), split))
                continue;

            bestStomach = stomach;
            highestAvailable = stomachSolution.AvailableVolume;
        }

        if (bestStomach == null)
        {
            _solution.TryAddSolution(solutionEntity.Value, split);
            return false;
        }

        _reactive.DoEntityReaction(wearer, split, ReactionMethod.Ingestion);
        _stomach.TryTransferSolution((bestStomach.Value.Owner, bestStomach.Value.Comp, null), split);

        if (solutionEntity.Value.Comp.Solution.Volume > FixedPoint2.Zero)
            return false;

        ent.Comp.NextConsume = TimeSpan.Zero;
        return true;
    }
}
