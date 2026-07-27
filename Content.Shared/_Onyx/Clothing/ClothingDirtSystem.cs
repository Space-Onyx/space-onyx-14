using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared._Onyx.Clothing;

public sealed partial class ClothingDirtSystem : EntitySystem
{
    public const string DefaultSolutionName = "dirt";
    public static readonly SlotFlags BleedSlots = SlotFlags.INNERCLOTHING;
    private static readonly SlotFlags UnderwearSlots = SlotFlags.UNDERWEART | SlotFlags.UNDERWEARB;

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private readonly HashSet<EntityUid> _drying = new();
    private readonly List<EntityUid> _dryingBuffer = new();
    private float _dryUpdateAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothingDirtableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ClothingDirtableComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ClothingDirtableComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ClothingDirtableComponent, SolutionChangedEvent>(OnSolutionChanged);
    }

    private void OnShutdown(Entity<ClothingDirtableComponent> ent, ref ComponentShutdown args)
        => _drying.Remove(ent.Owner);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer || (_dryUpdateAccumulator += frameTime) < 5f)
            return;

        var elapsed = _dryUpdateAccumulator;
        _dryUpdateAccumulator = 0f;
        _dryingBuffer.Clear();
        _dryingBuffer.AddRange(_drying);
        foreach (var uid in _dryingBuffer)
        {
            if (!TryComp(uid, out ClothingDirtableComponent? dirtable))
            {
                _drying.Remove(uid);
                continue;
            }

            dirtable.DryAccumulator += elapsed;
            if (dirtable.DryAccumulator < dirtable.DryInterval)
                continue;
            dirtable.DryAccumulator %= dirtable.DryInterval;
            DryClothing((uid, dirtable));
        }
    }

    private void OnMapInit(Entity<ClothingDirtableComponent> ent, ref MapInitEvent args)
    {
        if (_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solutionEnt, out var solution))
        {
            if (solution.MaxVolume != ent.Comp.Capacity)
                _solutions.SetCapacity(solutionEnt.Value, ent.Comp.Capacity);
            Refresh(ent, solution);
        }
    }

    private void OnSolutionChanged(Entity<ClothingDirtableComponent> ent, ref SolutionChangedEvent args)
    {
        if (!_net.IsServer || args.Solution.Comp.Id != ent.Comp.Solution)
            return;
        Refresh(ent, args.Solution.Comp.Solution);
    }

    private void OnExamined(Entity<ClothingDirtableComponent> ent, ref ExaminedEvent args)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution) || solution.Volume <= 0 ||
            solution.GetPrimaryReagentId() is not { } primaryId ||
            !_prototype.Resolve<ReagentPrototype>(primaryId.Prototype, out var primary))
            return;

        args.PushMarkup(Loc.GetString("clothing-dirtable-examine",
            ("color", solution.GetColor(_prototype).ToHexNoAlpha()),
            ("desc", primary.LocalizedPhysicalDescription),
            ("chemCount", solution.Contents.Count)));
    }

    public bool TryDirtyClothing(EntityUid clothing, Solution source, FixedPoint2 amount,
        ClothingDirtableComponent? component = null)
    {
        if (!_net.IsServer || amount <= 0 || source.Volume <= 0 ||
            !Resolve(clothing, ref component, false) ||
            !_solutions.TryGetSolution(clothing, component.Solution, out var solutionEnt, out var dirt))
            return false;

        var target = FixedPoint2.Min(amount, source.Volume, dirt.AvailableVolume);
        if (target <= 0)
            return false;

        var sample = new Solution();
        var sourceVolume = source.Volume;
        foreach (var reagent in source.Contents)
        {
            var accepted = FixedPoint2.Min(
                reagent.Quantity / sourceVolume * target,
                component.MaxReagentAmount - dirt.GetReagentQuantity(reagent.Reagent),
                target - sample.Volume);
            if (accepted > 0)
                sample.AddReagent(reagent.Reagent, accepted);
        }

        if (sample.Volume <= 0 || !_solutions.TryAddSolution(solutionEnt.Value, sample))
            return false;
        if (ProcessCleaners(dirt))
            _solutions.UpdateChemicals(solutionEnt.Value);
        Refresh((clothing, component), dirt);
        return true;
    }

    public bool TryAddCleanerToClothing(EntityUid clothing, ReagentId cleaner, FixedPoint2 amount,
        ClothingDirtableComponent? component = null)
    {
        if (!_net.IsServer || amount <= 0 || !Resolve(clothing, ref component, false) ||
            !_prototype.Resolve<ReagentPrototype>(cleaner.Prototype, out var prototype) ||
            prototype.ClothingDirtCleanMultiplier <= 0 ||
            !_solutions.TryGetSolution(clothing, component.Solution, out var solutionEnt, out var dirt))
            return false;

        var add = FixedPoint2.Min(amount,
            component.MaxReagentAmount - dirt.GetReagentQuantity(cleaner));
        if (add <= 0)
            return false;

        var spaceNeeded = FixedPoint2.Max(FixedPoint2.Zero, add - dirt.AvailableVolume);
        if (spaceNeeded > 0)
            RemoveWashableDirt(dirt, spaceNeeded);

        add = FixedPoint2.Min(add, dirt.AvailableVolume);
        if (add <= 0)
            return false;

        var solution = new Solution();
        solution.AddReagent(cleaner, add);
        if (!_solutions.TryAddSolution(solutionEnt.Value, solution))
            return false;

        if (ProcessCleaners(dirt))
            _solutions.UpdateChemicals(solutionEnt.Value);
        Refresh((clothing, component), dirt);
        return true;
    }

    public bool TryWashClothing(EntityUid clothing, ReagentId cleaner, FixedPoint2 amount,
        ClothingDirtableComponent? component = null)
    {
        if (!_net.IsServer || amount <= 0 || !Resolve(clothing, ref component, false) ||
            !_prototype.Resolve<ReagentPrototype>(cleaner.Prototype, out var prototype) ||
            prototype.ClothingDirtCleanMultiplier <= 0 ||
            !_solutions.TryGetSolution(clothing, component.Solution, out var solutionEnt, out var dirt))
            return false;

        var washable = dirt.Contents
            .Where(x => !IsCleaner(x.Reagent))
            .Aggregate(FixedPoint2.Zero, (total, x) => total + x.Quantity);
        var remaining = FixedPoint2.Min(amount * prototype.ClothingDirtCleanMultiplier, washable);
        if (remaining <= 0)
            return true;

        var original = remaining;
        foreach (var reagent in dirt.Contents.ToArray())
        {
            if (remaining <= 0 || IsCleaner(reagent.Reagent))
                continue;
            remaining -= dirt.RemoveReagent(reagent.Reagent, FixedPoint2.Min(reagent.Quantity / washable * original, remaining));
        }

        if (remaining > 0)
        {
            foreach (var reagent in dirt.Contents.ToArray())
            {
                if (remaining <= 0 || IsCleaner(reagent.Reagent))
                    continue;
                remaining -= dirt.RemoveReagent(reagent.Reagent, FixedPoint2.Min(reagent.Quantity, remaining));
            }
        }

        _solutions.UpdateChemicals(solutionEnt.Value);
        Refresh((clothing, component), dirt);
        return original > remaining;
    }

    public bool TryDirtyWorn(EntityUid wearer, Solution source, FixedPoint2 amount, SlotFlags slots)
        => TryDirtyLayer(wearer, source, amount, slots).Dirtied;

    public bool TryDirtyWornSplash(EntityUid wearer, Solution source, FixedPoint2 amount)
    {
        var dirtied = TryDirtyLayered(wearer, source, amount,
            SlotFlags.OUTERCLOTHING, SlotFlags.INNERCLOTHING, UnderwearSlots);
        dirtied |= TryDirtyLayered(wearer, source, amount, SlotFlags.FEET, SlotFlags.SOCKS);
        dirtied |= TryDirtyWorn(wearer, source, amount, SlotFlags.GLOVES);
        return dirtied;
    }

    public bool TryDirtyWornPuddleStep(EntityUid wearer, Solution source, FixedPoint2 amount)
        => TryDirtyLayered(wearer, source, amount, SlotFlags.FEET, SlotFlags.SOCKS);

    public bool TryDirtyWornPuddleCrawl(EntityUid wearer, Solution source, FixedPoint2 amount)
    {
        var dirtied = TryDirtyFirstOccupiedLayer(wearer, source, amount,
            SlotFlags.OUTERCLOTHING, SlotFlags.INNERCLOTHING, UnderwearSlots);
        dirtied |= TryDirtyFirstOccupiedLayer(wearer, source, amount, SlotFlags.FEET, SlotFlags.SOCKS);
        dirtied |= TryDirtyWorn(wearer, source, amount,
            SlotFlags.HEAD | SlotFlags.EARS | SlotFlags.MASK | SlotFlags.NECK |
            SlotFlags.BACK | SlotFlags.BELT | SlotFlags.GLOVES | SlotFlags.IDCARD | SlotFlags.SUITSTORAGE);
        return dirtied;
    }

    private bool TryDirtyFirstOccupiedLayer(EntityUid wearer, Solution source, FixedPoint2 amount,
        params SlotFlags[] layers)
    {
        foreach (var layer in layers)
        {
            var result = TryDirtyLayer(wearer, source, amount, layer);
            if (result.HadItem)
                return result.Dirtied;
        }
        return false;
    }

    private bool TryDirtyLayered(EntityUid wearer, Solution source, FixedPoint2 amount, params SlotFlags[] layers)
    {
        var dirtied = false;
        foreach (var layer in layers)
        {
            var result = TryDirtyLayer(wearer, source, amount, layer);
            if (!result.HadItem)
                continue;
            dirtied |= result.Dirtied;
            if (!result.DeepDirty)
                return dirtied;
            amount *= result.TransferFraction;
        }
        return dirtied;
    }

    private LayerResult TryDirtyLayer(EntityUid wearer, Solution source, FixedPoint2 amount, SlotFlags slots)
    {
        if (!_inventory.TryGetContainerSlotEnumerator(wearer, out var enumerator, slots))
            return default;

        var result = new LayerResult();
        while (enumerator.NextItem(out var item))
        {
            result.HadItem = true;
            if (!TryComp(item, out ClothingDirtableComponent? dirtable))
                continue;
            result.Dirtied |= TryDirtyClothing(item, source, amount, dirtable);
            if (_solutions.TryGetSolution(item, dirtable.Solution, out _, out var dirt) && IsDeepDirty(dirt, dirtable))
            {
                result.DeepDirty = true;
                result.TransferFraction = FixedPoint2.Max(result.TransferFraction, dirtable.DeepDirtTransferFraction);
            }
        }
        return result;
    }

    private void DryClothing(Entity<ClothingDirtableComponent> ent)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solutionEnt, out var dirt))
        {
            _drying.Remove(ent.Owner);
            return;
        }

        var changed = ProcessCleaners(dirt);
        foreach (var reagent in dirt.Contents.ToArray())
        {
            var minimum = _prototype.Resolve<ReagentPrototype>(reagent.Reagent.Prototype, out var prototype) &&
                          prototype.EvaporationSpeed > 0 ? FixedPoint2.Zero : ent.Comp.DryMinimum;
            var remove = FixedPoint2.Min(ent.Comp.DryAmount, reagent.Quantity - minimum);
            if (remove > 0)
                changed |= dirt.RemoveReagent(reagent.Reagent, remove) > 0;
        }

        if (changed)
            _solutions.UpdateChemicals(solutionEnt.Value);
        Refresh(ent, dirt);
    }

    private bool ProcessCleaners(Solution dirt)
    {
        var changed = false;
        foreach (var cleaner in dirt.Contents.ToArray())
        {
            if (!_prototype.Resolve<ReagentPrototype>(cleaner.Reagent.Prototype, out var prototype) ||
                prototype.ClothingDirtCleanMultiplier <= 0)
                continue;

            var washable = dirt.Contents
                .Where(x => !IsCleaner(x.Reagent))
                .Aggregate(FixedPoint2.Zero, (total, reagent) => total + reagent.Quantity);
            if (washable <= 0)
                break;

            var cleanAmount = FixedPoint2.Min(
                cleaner.Quantity * prototype.ClothingDirtCleanMultiplier,
                washable);
            var removed = RemoveWashableDirt(dirt, cleanAmount);
            if (removed <= 0)
                continue;

            dirt.RemoveReagent(cleaner.Reagent,
                FixedPoint2.Min(cleaner.Quantity, removed / prototype.ClothingDirtCleanMultiplier));
            changed = true;
        }

        return changed;
    }

    private FixedPoint2 RemoveWashableDirt(Solution dirt, FixedPoint2 amount)
    {
        var washable = dirt.Contents
            .Where(x => !IsCleaner(x.Reagent))
            .Aggregate(FixedPoint2.Zero, (total, reagent) => total + reagent.Quantity);
        var remaining = FixedPoint2.Min(amount, washable);
        var removed = FixedPoint2.Zero;
        if (remaining <= 0)
            return removed;

        var original = remaining;
        foreach (var reagent in dirt.Contents.ToArray())
        {
            if (remaining <= 0 || IsCleaner(reagent.Reagent))
                continue;
            var quantity = FixedPoint2.Min(reagent.Quantity / washable * original, remaining);
            var current = dirt.RemoveReagent(reagent.Reagent, quantity);
            removed += current;
            remaining -= current;
        }

        return removed;
    }

    private bool IsDeepDirty(Solution dirt, ClothingDirtableComponent component)
        => dirt.Volume >= FixedPoint2.Min(component.Capacity, component.MaxReagentAmount) * component.DeepDirtThreshold;

    private bool IsCleaner(ReagentId reagent)
        => _prototype.Resolve<ReagentPrototype>(reagent.Prototype, out var prototype) &&
           prototype.ClothingDirtCleanMultiplier > 0;

    private void Refresh(Entity<ClothingDirtableComponent> ent, Solution dirt)
    {
        var dryable = dirt.Contents.Any(x =>
            x.Quantity > (_prototype.Resolve<ReagentPrototype>(x.Reagent.Prototype, out var prototype) &&
                          prototype.EvaporationSpeed > 0 ? FixedPoint2.Zero : ent.Comp.DryMinimum));
        if (_net.IsServer)
        {
            if (dryable) _drying.Add(ent.Owner);
            else _drying.Remove(ent.Owner);
        }

        Color? color = null;
        if (dirt.Volume > 0 && ent.Comp.Capacity > 0)
        {
            var alpha = Math.Clamp(dirt.Volume.Float() / ent.Comp.Capacity.Float(),
                ent.Comp.MinVisualAlpha, ent.Comp.MaxVisualAlpha);
            color = dirt.GetColor(_prototype).WithAlpha(alpha);
        }
        if (ent.Comp.DirtColor == color)
            return;
        ent.Comp.DirtColor = color;
        Dirty(ent);
        _item.VisualsChanged(ent.Owner);
    }

    private struct LayerResult
    {
        public bool HadItem;
        public bool Dirtied;
        public bool DeepDirty;
        public FixedPoint2 TransferFraction;
    }
}
