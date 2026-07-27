using Content.Shared.CrusherUpgrades;
using Content.Shared.EntityEffects;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.CrusherUpgrades;

public sealed partial class CrusherUpgradeEffectsSystem : EntitySystem
{
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WeaponUpgradeEffectsComponent, MeleeHitEvent>(OnHit);
    }

    private void OnHit(Entity<WeaponUpgradeEffectsComponent> ent, ref MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
            _effects.ApplyEffects(target, ent.Comp.Effects, user: args.User);
    }
}
