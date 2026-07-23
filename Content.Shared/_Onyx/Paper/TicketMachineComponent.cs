using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Paper;

[RegisterComponent]
public sealed partial class TicketMachineComponent : Component
{
    [DataField]
    public int Queue;

    [DataField]
    public EntProtoId TicketPrototype = "PaperTicket";
}
