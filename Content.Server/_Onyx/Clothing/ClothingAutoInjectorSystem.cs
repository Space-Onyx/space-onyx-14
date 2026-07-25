using Content.Shared._Onyx.Clothing;
using Content.Shared._Onyx.Clothing.Components;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Clothing;

public sealed partial class ClothingAutoInjectorSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ClothingAutoInjectComponent, ActionActivateAutoInjectorEvent>(OnAction);
        SubscribeLocalEvent<ClothingAutoInjectComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ClothingAutoInjectComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ClothingAutoInjectComponent, InventoryRelayedEvent<ClothingAutoInjectRelayedEvent>>(OnRelayed);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnAction(Entity<ClothingAutoInjectComponent> ent, ref ActionActivateAutoInjectorEvent args)
    {
        if (args.Handled || !TryInject(args.Performer, ent))
            return;
        Feedback(args.Performer, ent.Comp);
        args.Handled = true;
    }

    private void OnGetActions(Entity<ClothingAutoInjectComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands && ent.Comp.AutoInjectOnAbility)
            args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnUnequipped(Entity<ClothingAutoInjectComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.AutoInjectOnAbility)
            _actions.RemoveProvidedActions(args.EquipTarget, ent.Owner);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (!TryComp<InventoryComponent>(args.Target, out var inventory))
            return;
        var ev = new ClothingAutoInjectRelayedEvent(args.Target, args.NewMobState);
        _inventory.RelayEvent((args.Target, inventory), ev);
    }

    private void OnRelayed(Entity<ClothingAutoInjectComponent> ent, ref InventoryRelayedEvent<ClothingAutoInjectRelayedEvent> args)
    {
        if (!ent.Comp.AutoInjectOnCrit || args.Args.NewState != MobState.Critical || ent.Comp.NextAutoInjectTime > _timing.CurTime || !TryInject(args.Args.Target, ent))
            return;
        Feedback(args.Args.Target, ent.Comp);
    }

    private bool TryInject(EntityUid target, Entity<ClothingAutoInjectComponent> clothing)
    {
        if (!_solutions.TryGetInjectableSolution(target, out var injectable, out _))
            return false;
        var solution = new Solution();
        foreach (var (reagent, amount) in clothing.Comp.Reagents)
            solution.AddReagent(reagent, amount);
        if (!_solutions.TryAddSolution(injectable.Value, solution))
            return false;
        clothing.Comp.NextAutoInjectTime = _timing.CurTime + clothing.Comp.AutoInjectInterval;
        Dirty(clothing);
        return true;
    }

    private void Feedback(EntityUid target, ClothingAutoInjectComponent component)
    {
        _audio.PlayPvs(component.InjectSound, target);
        _popup.PopupEntity(Loc.GetString(component.Popup), target, target);
    }
}
