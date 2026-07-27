using Content.Shared._Onyx.Disease.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.HealthAnalyzer.UI;

public sealed partial class HealthAnalyzerControl
{
    private void DrawDiseases(EntityUid target)
    {
        DiseasesContainer.RemoveAllChildren();
        if (!_entityManager.TryGetComponent<DiseaseCarrierComponent>(target, out var carrier) || carrier.Diseases.Count == 0)
        {
            DiseasesDivider.Visible = false;
            DiseasesContainer.Visible = false;
            return;
        }

        DiseasesDivider.Visible = true;
        DiseasesContainer.Visible = true;
        DiseasesContainer.AddChild(new RichTextLabel { Text = Loc.GetString("health-analyzer-window-diseases") });
        foreach (var diseaseUid in carrier.Diseases.ContainedEntities)
        {
            if (!_entityManager.TryGetComponent<DiseaseComponent>(diseaseUid, out var disease))
                continue;

            DiseasesContainer.AddChild(new RichTextLabel
            {
                Text = Loc.GetString("health-analyzer-window-disease-type-text", ("type", disease.Genotype)) + "\n · " +
                       Loc.GetString("health-analyzer-window-disease-progress-text", ("progress", disease.InfectionProgress)) + "\n · " +
                       Loc.GetString("health-analyzer-window-immunity-progress-text", ("progress", disease.ImmunityProgress)),
            });
        }
    }
}
