namespace Content.Server._Onyx.Telecommunications.Components;

[RegisterComponent]
public sealed partial class TelecomTrafficConsoleComponent : Component
{
    [DataField]
    public EntityUid? SelectedServer;

    public float TelemetryAccumulator;
    public float FullRefreshAccumulator;
    public int LastLogRevision = -1;
    public int LastTelemetryHash;
}
