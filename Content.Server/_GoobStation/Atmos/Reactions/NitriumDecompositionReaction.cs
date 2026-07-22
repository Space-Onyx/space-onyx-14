using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class NitriumDecompositionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var nitrium = mixture.GetMoles(Gas.Nitrium);
        var amount = Math.Min(mixture.Temperature / 2984f, nitrium);

        if (amount <= 0f)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Nitrium, -amount);
        mixture.AdjustMoles(Gas.WaterVapor, amount);
        mixture.AdjustMoles(Gas.Nitrogen, amount);

        var heatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max(
                (mixture.Temperature * heatCapacity + amount * Atmospherics.NitriumDecompositionEnergy) / heatCapacity,
                Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
