using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ProjectileThrowOnHitComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Speed = 10f;

    [DataField, AutoNetworkedField]
    public float Distance = 20f;

    [DataField, AutoNetworkedField]
    public bool UnanchorOnHit;

    [DataField, AutoNetworkedField]
    public TimeSpan? StunTime;
}
