using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Weapons.Multishot;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MissChanceComponent : Component
{
    [DataField, AutoNetworkedField] public float Chance = 0.35f;
}
