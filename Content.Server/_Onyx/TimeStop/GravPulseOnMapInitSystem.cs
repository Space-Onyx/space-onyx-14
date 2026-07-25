using Content.Server.Singularity.EntitySystems;

namespace Content.Server._Onyx.TimeStop;

public sealed partial class GravPulseOnMapInitSystem : EntitySystem
{
    [Dependency] private GravityWellSystem _gravityWell = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GravPulseOnMapInitComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<GravPulseOnMapInitComponent> ent, ref MapInitEvent args)
    {
        _gravityWell.GravPulse(ent,
            ent.Comp.MaxRange,
            ent.Comp.MinRange,
            ent.Comp.BaseRadialAcceleration,
            ent.Comp.BaseTangentialAcceleration);
    }
}
