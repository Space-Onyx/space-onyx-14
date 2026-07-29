using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.StationEvents;

[Prototype]
public sealed partial class EventTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}
