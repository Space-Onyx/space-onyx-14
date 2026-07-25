using System.Text;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Shared._Onyx.Clothing.Coating;

public sealed partial class ClothingCoatingSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothingCoatingComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CoatedClothingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SpeedModifierImmunityComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshSpeed);
    }

    private void OnAfterInteract(Entity<ClothingCoatingComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.Target.HasValue || !HasComp<ClothingComponent>(args.Target.Value))
            return;

        var target = args.Target.Value;
        EntityManager.AddComponents(target, ent.Comp.Components);
        var coated = EnsureComp<CoatedClothingComponent>(target);
        if (!coated.CoatingNames.Contains(ent.Comp.CoatingName))
            coated.CoatingNames.Add(ent.Comp.CoatingName);

        Dirty(target, coated);
        if (_containers.TryGetContainingContainer(target, out var container))
            _movement.RefreshMovementSpeedModifiers(container.Owner);
        _popup.PopupEntity(Loc.GetString("clothing-coating-success", ("target", target), ("source", ent.Owner)), target, args.User);
        QueueDel(ent);
        args.Handled = true;
    }

    private void OnExamined(Entity<CoatedClothingComponent> ent, ref ExaminedEvent args)
    {
        var coatings = new StringBuilder();
        for (var i = 0; i < ent.Comp.CoatingNames.Count; i++)
        {
            if (i > 0)
                coatings.Append(", ");
            coatings.Append(Loc.GetString(ent.Comp.CoatingNames[i]));
        }

        args.PushMarkup(Loc.GetString("clothing-coating-inspect", ("coatings", coatings.ToString())));
    }

    private void OnRefreshSpeed(Entity<SpeedModifierImmunityComponent> ent,
        ref InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        args.Args.GrantSlowdownImmunity();
    }
}
