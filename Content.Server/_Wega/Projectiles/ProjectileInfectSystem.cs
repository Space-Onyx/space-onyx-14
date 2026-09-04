using Content.Server._Onyx.Disease;
using Content.Shared.Projectiles;
using Robust.Shared.Random;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileInfectSystem : EntitySystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileInfectComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(EntityUid entity, ProjectileInfectComponent component, ref ProjectileHitEvent ev)
    {
        var shooter = ev.Shooter;
        if (shooter == null || shooter == ev.Target)
            return;

        if (!_random.Prob(component.Prob))
            return;

        _disease.TryInfect(ev.Target, component.Infection, out _, force: true);
    }
}
