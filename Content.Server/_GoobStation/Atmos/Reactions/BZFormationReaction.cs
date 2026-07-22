using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class BZFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var nitrousOxide = mixture.GetMoles(Gas.NitrousOxide);
        var plasma = mixture.GetMoles(Gas.Plasma);
        var pressure = mixture.Pressure;

        if (pressure <= 0f || nitrousOxide <= 0f || plasma <= 0f)
            return ReactionResult.NoReaction;

        var environmentEfficiency = mixture.Volume / pressure;
        var ratioEfficiency = Math.Min(nitrousOxide / plasma, 1f);
        var bzFormed = Math.Min(0.01f * ratioEfficiency * environmentEfficiency,
            Math.Min(nitrousOxide * 2.5f, plasma * 1.25f));
        var decomposition = Math.Max(4f * (plasma / (nitrousOxide + plasma) - 0.75f), 0f);
        var decomposed = 0.4f * bzFormed * decomposition;
        var bzAdded = bzFormed * (1f - decomposition);
        var nitrousOxideRemoved = 0.4f * bzFormed;
        var plasmaRemoved = 0.8f * bzFormed * (1f - decomposition);

        if (bzFormed <= 0f || nitrousOxideRemoved > nitrousOxide || plasmaRemoved > plasma)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.NitrousOxide, -nitrousOxideRemoved);
        mixture.AdjustMoles(Gas.Plasma, -plasmaRemoved);
        mixture.AdjustMoles(Gas.Nitrogen, decomposed);
        mixture.AdjustMoles(Gas.Oxygen, decomposed * 0.5f);
        mixture.AdjustMoles(Gas.BZ, bzAdded);

        AddEnergy(mixture, atmosphereSystem, bzFormed * (Atmospherics.BZFormationEnergy + decomposition));
        return ReactionResult.Reacting;
    }

    private static void AddEnergy(GasMixture mixture, AtmosphereSystem atmosphereSystem, float energy)
    {
        var heatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCapacity + energy) / heatCapacity, Atmospherics.TCMB);
    }
}
