using Content.Server.Anomaly.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Onyx.Research;
using Content.Shared.Research.Components;

namespace Content.Server.Anomaly;

public sealed partial class AnomalySystem
{
    private void InitializeAnomalyPointTypes()
    {
        SubscribeLocalEvent<AnomalyVesselComponent, ResearchServerGetPointsPerSecondByTypeEvent>(OnVesselGetPointsPerSecondByType);
    }

    /// <summary>
    /// Checks whether the anomaly redirects its research output into typed point rewards.
    /// </summary>
    private bool HasAnomalyTypedRewards(EntityUid anomaly)
    {
        if (!TryComp<AnomalyPointRewardsComponent>(anomaly, out var rewards))
            return false;

        foreach (var entry in rewards.PointTypes)
        {
            if (entry.Amount > 0)
                return true;
        }

        return false;
    }

    private void OnVesselGetPointsPerSecondByType(EntityUid uid, AnomalyVesselComponent component, ref ResearchServerGetPointsPerSecondByTypeEvent args)
    {
        if (!this.IsPowered(uid, EntityManager) || component.Anomaly is not { } anomaly)
            return;

        if (!TryComp<AnomalyPointRewardsComponent>(anomaly, out var rewards))
            return;

        var value = (int) (GetAnomalyPointValue(anomaly) * component.PointMultiplier);
        DistributeAnomalyPoints(args.Points, rewards, value);
    }

    /// <summary>
    /// Splits the vessel's point value across the configured point types proportionally
    /// to their weights, giving the rounding remainder to the last positive entry.
    /// </summary>
    private void DistributeAnomalyPoints(List<ResearchPointAmount> points, AnomalyPointRewardsComponent rewards, int value)
    {
        var totalWeight = 0;
        foreach (var entry in rewards.PointTypes)
        {
            if (entry.Amount > 0)
                totalWeight += entry.Amount;
        }

        if (value <= 0 || totalWeight <= 0)
            return;

        var remaining = value;
        var lastPositiveIndex = -1;
        for (var i = rewards.PointTypes.Count - 1; i >= 0; i--)
        {
            if (rewards.PointTypes[i].Amount > 0)
            {
                lastPositiveIndex = i;
                break;
            }
        }

        for (var i = 0; i < rewards.PointTypes.Count; i++)
        {
            var entry = rewards.PointTypes[i];
            if (entry.Amount <= 0)
                continue;

            var share = i == lastPositiveIndex
                ? remaining
                : value * entry.Amount / totalWeight;
            remaining -= share;

            if (share > 0)
                points.Add(new ResearchPointAmount(entry.Type, share));
        }
    }
}
