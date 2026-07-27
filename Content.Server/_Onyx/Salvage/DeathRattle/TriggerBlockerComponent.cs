using Content.Shared.Whitelist;

namespace Content.Server._Onyx.Salvage.DeathRattle;

[RegisterComponent]
public sealed partial class TriggerBlockerComponent : Component
{
    [DataField]
    public EntityWhitelist? MapWhitelist;

    [DataField]
    public EntityWhitelist? MapBlacklist;
}
