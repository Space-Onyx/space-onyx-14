using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
// <Onyx-Targeting>
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
// </Onyx-Targeting>

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private WoundDamageRoutingSystem _woundRouting = default!; // <Onyx-Targeting>

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanBasicDamageComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanBasicDamageComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        var dmg = ent.Comp.Damage * _damage.UniversalHitscanDamageModifier;

        // <Onyx-Targeting>
        // Shooter is the damage origin; snapshot supplies the fixed anatomical intent.
        bool damaged;
        DamageSpecifier damageDealt;
        if (TryComp(ent, out TargetingSnapshotComponent? snapshot) &&
            HasComp<WoundHostComponent>(args.Data.HitEntity.Value))
        {
            damaged = _woundRouting.TryApplyTargetedDamage(args.Data.HitEntity.Value,
                dmg,
                snapshot.RequestedTarget,
                snapshot.Shooter,
                out damageDealt);
        }
        else
        {
            damaged = _damage.TryChangeDamage(args.Data.HitEntity.Value,
                dmg,
                out damageDealt,
                origin: args.Data.Shooter);
        }
        // </Onyx-Targeting>
        if (!damaged)
            return;

        var damageEvent = new HitscanDamageDealtEvent
        {
            Target = args.Data.HitEntity.Value,
            DamageDealt = damageDealt,
        };

        RaiseLocalEvent(ent, ref damageEvent);
    }
}
