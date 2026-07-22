using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class NitriumProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var tritium = mixture.GetMoles(Gas.Tritium);
        var nitrogen = mixture.GetMoles(Gas.Nitrogen);
        var bz = mixture.GetMoles(Gas.BZ);
        var efficiency = Math.Min(mixture.Temperature / 2984f, Math.Min(bz * 20f, Math.Min(tritium, nitrogen)));
        var bzRemoved = efficiency * 0.05f;

        if (efficiency <= 0f || efficiency > tritium || efficiency > nitrogen || bzRemoved > bz)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Tritium, -efficiency);
        mixture.AdjustMoles(Gas.Nitrogen, -efficiency);
        mixture.AdjustMoles(Gas.BZ, -bzRemoved);
        mixture.AdjustMoles(Gas.Nitrium, efficiency);

        var heatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max(
                (mixture.Temperature * heatCapacity + efficiency * Atmospherics.NitriumProductionEnergy) / heatCapacity,
                Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
