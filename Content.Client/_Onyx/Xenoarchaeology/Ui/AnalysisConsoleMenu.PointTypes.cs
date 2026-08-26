using Content.Client.Message;
using Content.Client._Onyx.Research.UI;
using Content.Shared._Onyx.Research;
using Content.Shared.Research.Systems;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Xenoarchaeology.Ui;

public sealed partial class AnalysisConsoleMenu
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private SharedResearchSystem? _pointTypesResearch;

    private SharedResearchSystem PointTypesResearch => _pointTypesResearch ??= _ent.System<SharedResearchSystem>();

    /// <summary>
    /// Builds colored typed rewards markup appended to a node's extraction line.
    /// </summary>
    private string AppendNodeTypedRewards(Entity<XenoArtifactNodeComponent> node)
    {
        var rewards = _xenoArtifact.GetNodeTypedPointRewards(node);
        if (rewards.Count == 0)
            return string.Empty;

        return Loc.GetString("analysis-console-extract-typed-value",
            ("values", ResearchPointUiHelpers.BuildAbbreviatedBalanceMarkup(rewards, PointTypesResearch, _prototypes)));
    }

    /// <summary>
    /// Overrides the extraction summary with colored per-type totals of the extracted artifact.
    /// </summary>
    private void SetExtractionSumsLabel()
    {
        var sums = new List<ResearchPointAmount> { new(ResearchPointAmount.General, _extractionSum) };
        if (_artifactAnalyzer.TryGetArtifactFromConsole(_owner, out var artifact))
        {
            foreach (var node in _xenoArtifact.GetAllNodes(artifact.Value))
            {
                if (_xenoArtifact.GetResearchValue(node) <= 0)
                    continue;

                sums.AddRange(_xenoArtifact.GetNodeTypedPointRewards(node));
            }
        }

        ExtractionSumLabel.SetMarkup(Loc.GetString("analysis-console-extract-sum-typed",
            ("values", ResearchPointUiHelpers.BuildAbbreviatedBalanceMarkup(sums, PointTypesResearch, _prototypes))));
    }
}
