using Content.Server.Research.Systems;
using Content.Shared._Onyx.Research;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Systems;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Equipment.Components;

namespace Content.Server.Xenoarchaeology.Equipment;

public sealed partial class ArtifactAnalyzerSystem
{
    /// <summary>
    /// Drains the artifact nodes, awarding their research value as General points
    /// and their typed point rewards as-is. Plays extraction feedback when successful.
    /// </summary>
    private bool TryExtractTypedPoints(
        Entity<AnalysisConsoleComponent> ent,
        Entity<XenoArtifactComponent> artifact,
        EntityUid server,
        ResearchServerComponent? serverComponent)
    {
        var sumResearch = 0;
        var typedRewards = new List<ResearchPointAmount>();
        foreach (var node in _xenoArtifact.GetAllNodes(artifact))
        {
            var research = _xenoArtifact.GetResearchValue(node);
            if (research <= 0)
                continue;

            _xenoArtifact.SetConsumedResearchValue(node, node.Comp.ConsumedResearchValue + research);
            sumResearch += research;

            typedRewards.AddRange(_xenoArtifact.ClaimNodeTypedPointRewards(node));
        }

        // 4-16-25: It's a sad day when a scientist makes negative 5k research
        if (sumResearch <= 0)
            return false;

        _research.ModifyServerPoints(server, sumResearch, serverComponent);
        foreach (var reward in SharedResearchSystem.AggregatePoints(typedRewards))
        {
            if (reward.Amount <= 0)
                continue;

            _research.ModifyServerPoints(server, reward.Type, reward.Amount, serverComponent);
        }

        _audio.PlayPvs(ent.Comp.ExtractSound, artifact);
        _popup.PopupEntity(Loc.GetString("analyzer-artifact-extract-popup"), artifact, PopupType.Large);
        return true;
    }
}
