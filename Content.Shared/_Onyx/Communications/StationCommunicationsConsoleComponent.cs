using Content.Shared.AlertLevel;
using Content.Shared._Onyx.Screens;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Communications;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class StationCommunicationsConsoleComponent : Component
{
    [DataField]
    public LocId AnnouncementTitle = "comms-console-announcement-title-station";

    [DataField]
    public Color AnnouncementColor = Color.Gold;

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");

    [DataField]
    public bool GlobalAnnouncements;

    [DataField]
    public bool AnnounceSentBy = true;

    [DataField]
    public bool CanAnnounce = true;

    [DataField]
    public bool CanAlertLevel = true;

    [DataField]
    public bool CanCallShuttles = true;

    [DataField]
    public bool CanConfigureScreens = true;

    [DataField, AutoNetworkedField]
    public TimeSpan CanAnnounceAt = TimeSpan.Zero;

    [DataField]
    public TimeSpan AnnouncementInterval = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan InitialAnnouncementDelay = TimeSpan.FromSeconds(30);

    [DataField, AutoNetworkedField]
    public string CurrentAlertLevel = string.Empty;

    [DataField, AutoNetworkedField]
    public List<CommunicationsConsoleAlertLevel> AlertLevels = [];

    [DataField, AutoNetworkedField]
    public TimeSpan? CanSetAlertAt;

    [DataField, AutoNetworkedField]
    public bool ShuttlesCallable;

    [DataField, AutoNetworkedField]
    public TimeSpan? ExpectedEvacuationArrival;

    [DataField, AutoNetworkedField]
    public TimeSpan? ExpectedEvacuationDuration;

    [DataField, AutoNetworkedField]
    public StatusDisplayContent LastConfiguredContent = StatusDisplayContent.Text;

    [DataField, AutoNetworkedField]
    public bool LastConfiguredShowBorders;

    [DataField, AutoNetworkedField]
    public string LastConfiguredLine1 = string.Empty;

    [DataField, AutoNetworkedField]
    public string LastConfiguredLine2 = string.Empty;
}

[Serializable, NetSerializable]
public readonly record struct CommunicationsConsoleAlertLevel(LocId AlertLevel, LocId Description, string Id, bool CanSet, Color Color);

[Serializable, NetSerializable]
public sealed class CommunicationsConsoleEvacuationShuttleMessage(bool call) : BoundUserInterfaceMessage
{
    public readonly bool Call = call;
}

[Serializable, NetSerializable]
public sealed class CommunicationsConsoleAnnouncementMessage(string announcement) : BoundUserInterfaceMessage
{
    public readonly string Announcement = announcement;
}

[Serializable, NetSerializable]
public sealed class CommunicationsConsoleAlertLevelMessage(string alertLevel) : BoundUserInterfaceMessage
{
    public readonly string AlertLevel = alertLevel;
}

[Serializable, NetSerializable]
public sealed class CommunicationsConsoleScreenConfigurationMessage(StatusDisplayContent content, bool showBorder, string line1, string line2) : BoundUserInterfaceMessage
{
    public readonly StatusDisplayContent Content = content;
    public readonly bool ShowBorder = showBorder;
    public readonly string Line1 = line1;
    public readonly string Line2 = line2;
}

[Serializable, NetSerializable]
public enum StationCommunicationsConsoleUi : byte
{
    Key,
}

public static class ScreenPackets
{
    public const string Content = "station-screen-content";
    public const string Grid = "station-screen-grid";
    public const string ShowBorders = "station-screen-borders";
    public const string Text = "station-screen-text";
}
