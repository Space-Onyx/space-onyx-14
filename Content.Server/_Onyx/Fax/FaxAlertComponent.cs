using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Fax;

[RegisterComponent]
public sealed partial class FaxAlertComponent : Component
{
    [DataField]
    public ProtoId<RadioChannelPrototype> AlertChannel = "Command";
}
