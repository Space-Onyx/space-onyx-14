using Content.Shared.Damage;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Power.PTL;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PTLComponent : Component
{
    [DataField, AutoNetworkedField] public bool Active;
    [DataField, AutoNetworkedField] public double SpesosHeld;
    [DataField] public double MinShootPower = 1e6;
    [DataField] public double MaxEnergyPerShot = 5e6;
    [DataField, AutoNetworkedField] public float ShootDelay = 10f;
    [DataField, AutoNetworkedField] public float ShootDelayIncrement = 5f;
    [DataField, AutoNetworkedField] public MinMax ShootDelayThreshold = new(10, 60);
    [DataField, AutoNetworkedField] public bool ReversedFiring;
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField] public TimeSpan NextShotAt;
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan RadDecayTimer;
    [DataField(required: true)] public DamageSpecifier BaseBeamDamage = default!;
    [DataField] public double EvilMultiplier = 0.1;
}
