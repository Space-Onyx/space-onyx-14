using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class HealiumProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var bz = mixture.GetMoles(Gas.BZ);
        var frezon = mixture.GetMoles(Gas.Frezon);
        var efficiency = Math.Min(mixture.Temperature * 0.3f, Math.Min(frezon * 0.36f, bz * 4f));
        var bzRemoved = efficiency * 0.25f;
        var frezonRemoved = efficiency * 2.75f;

        if (efficiency <= 0f || bzRemoved > bz || frezonRemoved > frezon)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.BZ, -bzRemoved);
        mixture.AdjustMoles(Gas.Frezon, -frezonRemoved);
        mixture.AdjustMoles(Gas.Healium, efficiency * 3f);

        var heatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max(
                (mixture.Temperature * heatCapacity + efficiency * Atmospherics.HealiumProductionEnergy) / heatCapacity,
                Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
