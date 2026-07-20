using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Disease.Components;

[RegisterComponent]
public sealed partial class DiseaseGrantComponentEffectComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components;

    [DataField]
    public bool RemoveOnCure = true;
}
