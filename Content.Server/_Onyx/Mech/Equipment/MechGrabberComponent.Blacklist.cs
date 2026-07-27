using Content.Shared.Whitelist;

namespace Content.Server.Mech.Equipment.Components;

public sealed partial class MechGrabberComponent
{
    [DataField]
    public EntityWhitelist Blacklist = new();
}
