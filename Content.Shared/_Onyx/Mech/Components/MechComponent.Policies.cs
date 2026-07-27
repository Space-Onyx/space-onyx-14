using Content.Shared.Whitelist;

namespace Content.Shared.Mech.Components;

public sealed partial class MechComponent
{
    [DataField]
    public bool BreakOnEmag = true;

    [DataField]
    public EntityWhitelist? PilotBlacklist;
}
