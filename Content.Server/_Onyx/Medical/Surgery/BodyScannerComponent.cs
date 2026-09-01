using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Medical.Surgery;

[RegisterComponent]
public sealed partial class BodyScannerComponent : Component
{
    public static readonly ProtoId<SourcePortPrototype> LinkingPort = "BodyScannerSender";

    [ViewVariables]
    public EntityUid? Table;
}
