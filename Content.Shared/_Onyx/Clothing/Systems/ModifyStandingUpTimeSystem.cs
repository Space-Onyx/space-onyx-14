using Content.Shared._Onyx.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared._Onyx.Clothing;

namespace Content.Shared._Onyx.Clothing.Systems;

public sealed partial class ModifyStandingUpTimeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ModifyStandingUpTimeComponent, GetStandingUpTimeMultiplierEvent>(OnGetMultiplier);
        SubscribeLocalEvent<ModifyStandingUpTimeComponent, InventoryRelayedEvent<GetStandingUpTimeMultiplierEvent>>(OnInventoryGetMultiplier);
    }

    private void OnGetMultiplier(Entity<ModifyStandingUpTimeComponent> ent, ref GetStandingUpTimeMultiplierEvent args) => args.Multiplier *= ent.Comp.Multiplier;
    private void OnInventoryGetMultiplier(Entity<ModifyStandingUpTimeComponent> ent, ref InventoryRelayedEvent<GetStandingUpTimeMultiplierEvent> args) => args.Args.Multiplier *= ent.Comp.Multiplier;
}
