using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CrusherUpgrades;

[RegisterComponent, NetworkedComponent]
public sealed partial class ItemUpgradeableComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class ItemUpgradeComponent : Component
{
    [DataField(required: true)] public LocId Name;
    [DataField] public LocId? ExamineTextType = "crusher-upgrade-examine";
    [DataField] public LocId? InsertedTextType = "crusher-upgrade-inserted";
    [DataField] public string? UniqueGroup;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherUpgradeComponentsComponent : Component
{
    [DataField] public ComponentRegistry Components = new();
}

[RegisterComponent]
public sealed partial class CrusherUpgradeOwnershipComponent : Component
{
    [DataField] public Dictionary<string, int> References = new();
    [DataField] public HashSet<string> AddedComponents = new();
}

[RegisterComponent, NetworkedComponent]
public sealed partial class WeaponUpgradeDamageComponent : Component
{
    [DataField] public DamageSpecifier BonusDamage = new();
}

[RegisterComponent, NetworkedComponent]
public sealed partial class WeaponUpgradeSpeedComponent : Component
{
    [DataField] public float AttackRateMultiplier = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class WeaponUpgradeRangeComponent : Component
{
    [DataField] public float RangeMultiplier = 1f;
}

[RegisterComponent]
public sealed partial class WeaponUpgradeEffectsComponent : Component
{
    [DataField] public EntityEffect[] Effects = Array.Empty<EntityEffect>();
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MapRestrictedComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class MapRestrictedGunComponent : Component
{
    [DataField] public LocId? PopupOnBlock;
}
