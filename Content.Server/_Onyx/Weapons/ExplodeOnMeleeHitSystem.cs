using Content.Server.Explosion.EntitySystems;
using Content.Shared.Explosion.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Onyx.Weapons;

public sealed partial class ExplodeOnMeleeHitSystem : EntitySystem
{
    [Dependency] private ExplosionSystem _explosion = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExplodeOnMeleeHitComponent, MeleeHitEvent>(OnHit);
    }

    private void OnHit(Entity<ExplodeOnMeleeHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !TryComp(ent, out ExplosiveComponent? explosive))
            return;

        foreach (var target in args.HitEntities)
        {
            _explosion.QueueExplosion(target,
                explosive.ExplosionType,
                explosive.TotalIntensity,
                explosive.IntensitySlope,
                explosive.MaxIntensity,
                explosive.TileBreakScale,
                explosive.MaxTileBreak,
                explosive.CanCreateVacuum,
                args.User);
        }
    }
}
