using Content.Server._Onyx.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Robust.Shared.Spawners;

namespace Content.Server._Onyx.Atmos.EntitySystems;

public sealed partial class AtmosResinDespawnSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private GasTileOverlaySystem _gasOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AtmosResinDespawnComponent, TimedDespawnEvent>(OnDespawn);
    }

    private void OnDespawn(Entity<AtmosResinDespawnComponent> ent, ref TimedDespawnEvent args)
    {
        var mixture = _atmosphere.GetContainingMixture(ent.Owner, true);
        if (mixture == null)
            return;

        var cleanMixture = new GasMixture();
        cleanMixture.AdjustMoles(0, mixture.GetMoles(0));
        cleanMixture.AdjustMoles(1, mixture.GetMoles(1));
        mixture.Remove(mixture.TotalMoles);
        _atmosphere.Merge(mixture, cleanMixture);
        mixture.Temperature = Atmospherics.T20C;
        _gasOverlay.UpdateSessions();
    }
}
