using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.GrabIntent;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GrabIntentComponent : Component
{
    [DataField, AutoNetworkedField]
    public GrabStage GrabStage;

    [DataField]
    public TimeSpan StageChangeCooldown = TimeSpan.FromSeconds(1);

    [AutoNetworkedField]
    public TimeSpan NextStageChange;

    [DataField]
    public Dictionary<GrabStage, float> EscapeChances = new()
    {
        { GrabStage.Soft, 1f },
        { GrabStage.Hard, 0.6f },
        { GrabStage.Suffocate, 0.2f },
    };
}
