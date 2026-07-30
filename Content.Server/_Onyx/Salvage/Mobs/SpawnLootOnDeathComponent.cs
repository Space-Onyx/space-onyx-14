using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Whitelist;

namespace Content.Server._Onyx.Salvage.Mobs;

[RegisterComponent]
public sealed partial class SpawnLootOnDeathComponent : Component
{
    [DataField]
    public EntityTableSelector? Table;

    [DataField]
    public EntityTableSelector? SpecialTable;

    [DataField]
    public EntityWhitelist? SpecialWeaponWhitelist;

    [DataField]
    public bool DeleteOnDeath;

    [DataField]
    public bool DropBoth;

    [ViewVariables]
    public bool DoSpecialLoot = true;
}
