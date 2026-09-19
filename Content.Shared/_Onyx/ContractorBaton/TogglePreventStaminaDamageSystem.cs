// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using Content.Shared.Damage.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Shared._Onyx.ContractorBaton;

public sealed partial class TogglePreventStaminaDamageSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TogglePreventStaminaDamageComponent, StaminaDamageOnHitAttemptEvent>(OnStaminaHitAttempt);
    }

    private void OnStaminaHitAttempt(Entity<TogglePreventStaminaDamageComponent> ent,
        ref StaminaDamageOnHitAttemptEvent args)
    {
        if (!TryComp<ItemToggleComponent>(ent, out var toggle) || !_toggle.IsActivated((ent.Owner, toggle)))
            args.Cancelled = true;
    }
}
