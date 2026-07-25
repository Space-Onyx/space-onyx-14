using Content.Shared.Whitelist;

namespace Content.Server._Onyx.Singularity;

[RegisterComponent]
public sealed partial class EventHorizonIgnoreComponent : Component
{
    [DataField]
    public EntityWhitelist? HorizonWhitelist;
}
