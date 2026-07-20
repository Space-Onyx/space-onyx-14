using Content.Server.Popups;
using Content.Shared._Onyx.Clothing;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._Onyx.Clothing;

public sealed partial class ClothingDirtWasherSystem : EntitySystem
{
    [Dependency] private ClothingDirtSystem _dirt = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothingDirtWasherComponent, AfterInteractUsingEvent>(OnUsing);
        SubscribeLocalEvent<ClothingDirtWasherComponent, WashClothingDoAfterEvent>(OnWash);
    }

    private void OnUsing(Entity<ClothingDirtWasherComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || !HasComp<ClothingDirtableComponent>(args.Used) ||
            !_solutions.TryGetDrainableSolution(ent.Owner, out _, out var washer) ||
            washer.GetTotalPrototypeQuantity(ent.Comp.CleanerReagent) <= 0)
            return;

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.WashTime,
                new WashClothingDoAfterEvent(), ent.Owner, target: ent.Owner, used: args.Used)
            { BreakOnMove = true, BreakOnDamage = true, NeedHand = true }))
            return;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("clothing-dirt-washing-self",
            ("clothing", Identity.Entity(args.Used, EntityManager))), args.User, args.User, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("clothing-dirt-washing-others",
            ("user", Identity.Entity(args.User, EntityManager)),
            ("clothing", Identity.Entity(args.Used, EntityManager))),
            args.User, Filter.PvsExcept(args.User), true, PopupType.Medium);
    }

    private void OnWash(Entity<ClothingDirtWasherComponent> ent, ref WashClothingDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } clothing ||
            !_solutions.TryGetDrainableSolution(ent.Owner, out var washerEnt, out var washer))
            return;
        var amount = FixedPoint2.Min(ent.Comp.Amount, washer.GetTotalPrototypeQuantity(ent.Comp.CleanerReagent));
        if (!_dirt.TryWashClothing(clothing, new ReagentId(ent.Comp.CleanerReagent, null), amount))
            return;
        washer.RemoveReagent(ent.Comp.CleanerReagent, amount, ignoreReagentData: true);
        _solutions.UpdateChemicals(washerEnt.Value);
        args.Handled = true;
    }
}
