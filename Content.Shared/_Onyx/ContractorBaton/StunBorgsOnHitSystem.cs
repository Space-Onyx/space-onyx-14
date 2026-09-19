// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using System.Linq;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Jittering;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Onyx.ContractorBaton;

public sealed partial class StunBorgsOnHitSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunBorgsOnHitComponent, MeleeHitEvent>(OnHit);
    }

    private void OnHit(Entity<StunBorgsOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!TryComp<ItemToggleComponent>(ent, out var toggle) || !_toggle.IsActivated((ent.Owner, toggle)))
            return;

        foreach (var borg in args.HitEntities.Where(HasComp<BorgChassisComponent>))
        {
            _stun.TryUpdateParalyzeDuration(borg, ent.Comp.ParalyzeDuration);
            _jitter.DoJitter(borg, ent.Comp.ParalyzeDuration, true);
        }
    }
}
