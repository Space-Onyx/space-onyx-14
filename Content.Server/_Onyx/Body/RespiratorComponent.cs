namespace Content.Server.Body.Components;

public sealed partial class RespiratorComponent
{
    /// <summary>
    /// Multiplier for passive saturation loss. Lower values reduce air consumption.
    /// </summary>
    [DataField]
    public float SaturationLoss = 1f;
}
