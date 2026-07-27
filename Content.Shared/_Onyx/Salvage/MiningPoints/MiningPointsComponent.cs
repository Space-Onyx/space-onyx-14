using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Salvage.MiningPoints;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MiningPointsComponent : Component
{
    [DataField, AutoNetworkedField]
    public long HalfUnits;

    public int Points => (int) Math.Clamp(HalfUnits / 2, 0, int.MaxValue);
}
