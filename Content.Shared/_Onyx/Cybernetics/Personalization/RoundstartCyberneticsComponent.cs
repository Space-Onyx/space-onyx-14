using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Cybernetics.Personalization;

[RegisterComponent]
public sealed partial class RoundstartCyberneticsComponent : Component
{
    [DataField]
    public int Cost = 1;

    [DataField]
    public List<EntProtoId> Dependencies = [];

    [DataField]
    public bool Selectable = true;
}
