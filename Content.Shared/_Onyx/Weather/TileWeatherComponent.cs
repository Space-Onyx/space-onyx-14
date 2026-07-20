using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Weather;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TileWeatherComponent : Component
{
    public const int ChunkSize = 8;

    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, ulong> Disabled = new();

    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, ulong> Enabled = new();
}
