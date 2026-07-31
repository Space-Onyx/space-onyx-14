using Content.Server.Chemistry.Components;
using Content.Shared._Onyx.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Throwing;

namespace Content.Server._Onyx.Weapons.Ranged;

public sealed class SyringeGunSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SyringeGunComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<SyringeGunComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<SyringeGunProjectileComponent, LandEvent>(OnLand);
    }

    private void OnAttemptShoot(Entity<SyringeGunComponent> ent, ref AttemptShootEvent args)
    {
        args.ThrowItems = true;
    }

    private void OnShot(Entity<SyringeGunComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp(projectile, out SolutionInjectWhileEmbeddedComponent? injection))
            {
                var fired = EnsureComp<SyringeGunProjectileComponent>(projectile);
                fired.OriginalUpdateInterval = injection.UpdateInterval;
                injection.UpdateInterval /= ent.Comp.InjectionSpeedMultiplier;
            }
        }
    }

    private void OnLand(Entity<SyringeGunProjectileComponent> ent, ref LandEvent args)
    {
        if (TryComp(ent, out SolutionInjectWhileEmbeddedComponent? injection))
            injection.UpdateInterval = ent.Comp.OriginalUpdateInterval;

        RemCompDeferred<SyringeGunProjectileComponent>(ent);
    }
}
