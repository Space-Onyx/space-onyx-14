using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Surgery.Organs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EyesComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Damage;

    [DataField, AutoNetworkedField]
    public int MinDamage;
}
