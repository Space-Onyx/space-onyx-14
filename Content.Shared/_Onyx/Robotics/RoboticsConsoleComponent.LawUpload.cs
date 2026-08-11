namespace Content.Shared.Robotics.Components;

public sealed partial class RoboticsConsoleComponent
{
    [DataField]
    public bool AllowLawUpload;

    [DataField]
    public string LawboardSlot = "lawboard";

    [DataField]
    public LocId ChangeLawsMessage = "robotics-console-cyborg-change-laws";

    public readonly Dictionary<string, EntityUid> LawUploadTargets = new();
}
