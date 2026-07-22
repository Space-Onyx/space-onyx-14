using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class PluoxiumProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var oxygen = mixture.GetMoles(Gas.Oxygen);
        var carbonDioxide = mixture.GetMoles(Gas.CarbonDioxide);
        var tritium = mixture.GetMoles(Gas.Tritium);
        var amount = Math.Min(5f, Math.Min(carbonDioxide, Math.Min(oxygen * 2f, tritium * 100f)));

        if (amount <= 0f)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.CarbonDioxide, -amount);
        mixture.AdjustMoles(Gas.Oxygen, -amount * 0.5f);
        mixture.AdjustMoles(Gas.Tritium, -amount * 0.01f);
        mixture.AdjustMoles(Gas.Pluoxium, amount);
        mixture.AdjustMoles(Gas.WaterVapor, amount * 0.01f);

        var heatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max(
                (mixture.Temperature * heatCapacity + amount * Atmospherics.PluoxiumProductionEnergy) / heatCapacity,
                Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
