using Robust.Shared.Audio;

namespace Content.Server._Onyx.Xenomorphs.GameTicking.Rules;

[RegisterComponent]
public sealed partial class XenomorphsRuleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Xenomorphs = new();

    [DataField]
    public TimeSpan CheckDelay = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan NextCheck;

    [DataField]
    public string? Announcement = "xenomorphs-announcement";

    [DataField]
    public SoundSpecifier XenomorphInfestationSound =
        new SoundPathSpecifier("/Audio/_Onyx/Music/Black_Swarm_Short.ogg")
        {
            Params = AudioParams.Default.WithVolume(-8f),
        };

    [DataField]
    public SoundSpecifier XenomorphTakeoverSound =
        new SoundPathSpecifier("/Audio/_Onyx/Music/mind_crawler_short.ogg")
        {
            Params = AudioParams.Default.WithVolume(-8f),
        };

    [DataField]
    public Color AnnouncementColor = Color.Red;

    [DataField]
    public string? NoMoreThreatAnnouncement = "xenomorphs-no-more-threat-announcement";

    [DataField]
    public Color NoMoreThreatAnnouncementColor = Color.Gold;

    [DataField]
    public string? Sender;

    [DataField]
    public TimeSpan MinTimeToAnnouncement = TimeSpan.FromSeconds(400);

    [DataField]
    public TimeSpan MaxTimeToAnnouncement = TimeSpan.FromSeconds(450);

    [ViewVariables]
    public bool Announced;

    [ViewVariables]
    public TimeSpan? AnnouncementTime;

    [DataField]
    public float XenomorphsShuttleCallPercentage = 0.7f;

    [DataField]
    public TimeSpan ShuttleCallTime = TimeSpan.FromMinutes(5);

    [DataField]
    public string RoundEndTextSender = "comms-console-announcement-title-centcom";

    [DataField]
    public string RoundEndTextShuttleCall = "xenomorphs-win-announcement-shuttle-call";

    [DataField]
    public string RoundEndTextAnnouncement = "xenomorphs-win-announcement";

    [DataField]
    public WinType WinType = WinType.Neutral;

    [DataField]
    public List<WinCondition> WinConditions = new();
}

public enum WinType : byte
{
    XenoMajor,
    XenoMinor,
    Neutral,
    CrewMinor,
    CrewMajor,
}

public enum WinCondition : byte
{
    NukeExplodedOnStation,
    NukeActiveInStation,
    XenoTakeoverStation,
    XenoInfiltratedOnCentCom,
    AllReproduceXenoDead,
    AllCrewDead,
}
