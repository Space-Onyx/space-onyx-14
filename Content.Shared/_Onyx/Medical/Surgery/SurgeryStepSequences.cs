using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

[DataDefinition]
public sealed partial class SurgeryStepSequence
{
    [DataField] public ComponentRegistry Required = new();
    [DataField(required: true)] public List<EntProtoId> Steps = new();
}
