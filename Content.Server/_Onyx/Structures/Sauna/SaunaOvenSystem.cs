// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Atmos.EntitySystems;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._Onyx.Structures.Sauna;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage.Components;
using Content.Shared.Toggleable;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Structures.Sauna;

public sealed partial class SaunaOvenSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private StackSystem _stack = default!;

    private static readonly ProtoId<StackPrototype> WoodPlank = "WoodPlank";
    private const string Water = "Water";
    private static readonly FixedPoint2 WaterTransfer = FixedPoint2.New(5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SaunaOvenComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SaunaOvenComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<SaunaOvenComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SaunaOvenComponent, ExaminedEvent>(OnExamined);
    }

    private void OnStartup(Entity<SaunaOvenComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Lit)
            EnsureComp<ActiveSaunaOvenComponent>(ent);
        UpdateAppearance(ent);
    }

    private void OnInteractHand(Entity<SaunaOvenComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !TryToggle(ent, args.User))
            return;

        args.Handled = true;
    }

    private void OnInteractUsing(Entity<SaunaOvenComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryAddFuelOrWater(ent, args.Used, args.User))
            return;

        args.Handled = true;
    }

    private void OnExamined(Entity<SaunaOvenComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Water > 0f
            ? "sauna-oven-examine-stones-wet"
            : "sauna-oven-examine-stones-dry"));
        args.PushMarkup(Loc.GetString(ent.Comp.Fuel > 0f
            ? "sauna-oven-examine-fuel-present"
            : "sauna-oven-examine-fuel-empty"));
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ActiveSaunaOvenComponent, SaunaOvenComponent>();
        while (query.MoveNext(out var uid, out _, out var oven))
        {
            oven.ProcessAccumulator += frameTime;
            if (oven.ProcessAccumulator < oven.ProcessInterval)
                continue;

            var elapsed = oven.ProcessAccumulator;
            oven.ProcessAccumulator %= oven.ProcessInterval;
            Process((uid, oven), elapsed);
        }
    }

    public bool TryToggle(Entity<SaunaOvenComponent> ent, EntityUid user)
    {
        if (!CanToggle(ent, user))
            return false;

        SetLit(ent, !ent.Comp.Lit);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Lit ? "sauna-oven-lit" : "sauna-oven-extinguished"), ent, user);
        return true;
    }

    public bool CanToggle(Entity<SaunaOvenComponent> ent, EntityUid user, bool quiet = false)
    {
        if (Deleted(ent) || ent.Comp.Lit || ent.Comp.Fuel > 0f)
            return !Deleted(ent);

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("sauna-oven-no-fuel"), ent, user);
        return false;
    }

    public bool TryAddFuelOrWater(Entity<SaunaOvenComponent> ent, EntityUid used, EntityUid user)
    {
        if (TryComp<StackComponent>(used, out var stack) && stack.StackTypeId == WoodPlank)
            return TryAddWood(ent, (used, stack), user);

        if (TryComp<BinComponent>(used, out var bin))
            return TryAddPaperBin(ent, (used, bin), user);

        if (HasComp<PaperComponent>(used))
            return TryAddPaper(ent, used, user);

        return TryAddWater(ent, used, user);
    }

    private bool TryAddWood(Entity<SaunaOvenComponent> ent, Entity<StackComponent> wood, EntityUid user)
    {
        if (ent.Comp.Fuel >= ent.Comp.MaximumFuel)
        {
            _popup.PopupEntity(Loc.GetString("sauna-oven-full"), ent, user);
            return true;
        }

        var amount = Math.Min(wood.Comp.Count, (int) MathF.Ceiling((ent.Comp.MaximumFuel - ent.Comp.Fuel) / ent.Comp.WoodFuel));
        if (amount <= 0 || !_stack.TryUse(wood.AsNullable(), amount))
            return false;

        ent.Comp.Fuel = Math.Min(ent.Comp.MaximumFuel, ent.Comp.Fuel + amount * ent.Comp.WoodFuel);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("sauna-oven-add-wood"), ent, user);
        return true;
    }

    private bool TryAddPaperBin(Entity<SaunaOvenComponent> ent, Entity<BinComponent> bin, EntityUid user)
    {
        if (Prototype(bin)?.ID.StartsWith("PaperBin", StringComparison.Ordinal) != true)
            return false;

        var count = bin.Comp.Items.Count;
        if (count == 0)
            return false;

        ent.Comp.Fuel += count * ent.Comp.PaperFuel;
        QueueDel(bin);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("sauna-oven-add-paper"), ent, user);
        return true;
    }

    private bool TryAddPaper(Entity<SaunaOvenComponent> ent, EntityUid paper, EntityUid user)
    {
        ent.Comp.Fuel += ent.Comp.PaperFuel;
        QueueDel(paper);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("sauna-oven-add-paper"), ent, user);
        return true;
    }

    private bool TryAddWater(Entity<SaunaOvenComponent> ent, EntityUid used, EntityUid user)
    {
        if (_openable.IsClosed(used, user) ||
            !_solutions.TryGetDrainableSolution(used, out var solutionEnt, out var solution))
            return false;

        var amount = FixedPoint2.Min(WaterTransfer, solution.GetTotalPrototypeQuantity(Water));
        if (amount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("sauna-oven-no-water"), ent, user);
            return true;
        }

        _solutions.RemoveReagent(solutionEnt.Value, Water, amount);
        ent.Comp.Water += amount.Float() * ent.Comp.WaterSteamMultiplier;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("sauna-oven-add-water"), ent, user);
        return true;
    }

    private void Process(Entity<SaunaOvenComponent> ent, float elapsed)
    {
        ent.Comp.Fuel -= ent.Comp.FuelConsumptionPerSecond * elapsed;
        if (ent.Comp.Water > 0f)
        {
            var consumed = Math.Min(ent.Comp.Water, ent.Comp.WaterConsumptionPerSecond * elapsed);
            ent.Comp.Water -= consumed;
            Spawn(ent.Comp.SteamEffect, Transform(ent).Coordinates);

            var atmosphere = _atmosphere.GetTileMixture((ent.Owner, Transform(ent)), true);
            if (atmosphere is { Immutable: false } && atmosphere.Pressure < ent.Comp.MaximumSteamPressure)
            {
                var steam = new GasMixture
                {
                    Temperature = ent.Comp.SteamTemperature,
                };
                steam.AdjustMoles(Gas.WaterVapor, ent.Comp.SteamMolesPerSecond * elapsed);
                _atmosphere.Merge(atmosphere, steam);
            }
        }

        if (ent.Comp.Fuel <= 0f)
        {
            ent.Comp.Fuel = 0f;
            SetLit(ent, false);
            return;
        }

        Dirty(ent);
    }

    private void SetLit(Entity<SaunaOvenComponent> ent, bool lit)
    {
        ent.Comp.Lit = lit;
        ent.Comp.ProcessAccumulator = 0f;
        if (lit)
            EnsureComp<ActiveSaunaOvenComponent>(ent);
        else
            RemCompDeferred<ActiveSaunaOvenComponent>(ent);
        Dirty(ent);
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<SaunaOvenComponent> ent)
    {
        _appearance.SetData(ent, SaunaOvenVisuals.Lit, ent.Comp.Lit);
        _appearance.SetData(ent, ToggleableVisuals.Enabled, ent.Comp.Lit);
    }
}
