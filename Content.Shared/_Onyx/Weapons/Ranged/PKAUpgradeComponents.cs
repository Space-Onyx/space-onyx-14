using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Weapons.Ranged;

[RegisterComponent, NetworkedComponent]
public sealed partial class GunUpgradePressureComponent : Component
{
    [DataField] public float? NewLowerBound;
    [DataField] public float? NewUpperBound;
    [DataField] public bool? NewApplyWhenInRange = true;
    [DataField] public float? NewAppliedModifier = 2f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class GunUpgradeVampirismComponent : Component
{
    [DataField] public DamageSpecifier DamageOnHit = new();
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ProjectileVampirismComponent : Component
{
    [DataField] public DamageSpecifier DamageOnHit = new();
}

[RegisterComponent, NetworkedComponent]
public sealed partial class PKAUpgradeEjectableComponent : Component
{
    [DataField] public TimeSpan EjectDelay = TimeSpan.FromSeconds(0.9);

    [DataField]
    public SoundSpecifier? EjectSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagOut/revolver_magout.ogg");
}

[Serializable, NetSerializable]
public sealed partial class PKAUpgradeEjectDoAfterEvent : SimpleDoAfterEvent
{
    public readonly NetEntity Upgrade;

    public PKAUpgradeEjectDoAfterEvent(NetEntity upgrade)
    {
        Upgrade = upgrade;
    }
}

[ByRefEvent]
public record struct RechargeBasicEntityAmmoGetCooldownModifiersEvent(float Cooldown);
