using Content.Shared.Eui;
using Robust.Shared.Map; // <Onyx-AdminAnnouncements>
using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{
    public enum AdminAnnounceType
    {
        // <Onyx-AdminAnnouncements-edited>
        AllStations,
        SpecificStation,
        SpecificMap,
        // </Onyx-AdminAnnouncements-edited>
        Server,
    }

    [Serializable, NetSerializable]
    public sealed class AdminAnnounceEuiState : EuiStateBase
    {
        // <Onyx-AdminAnnouncements>
        public Dictionary<NetEntity, string> Stations = new();
        public Dictionary<MapId, string> Maps = new();
        // </Onyx-AdminAnnouncements>
    }

    public static class AdminAnnounceEuiMsg
    {
        [Serializable, NetSerializable]
        public sealed class DoAnnounce : EuiMessageBase
        {
            public bool CloseAfter;
            public string Announcer = default!;
            public string Announcement = default!;
            public AdminAnnounceType AnnounceType;
            // <Onyx-AdminAnnouncements>
            public NetEntity? SelectedStation;
            public MapId? SelectedMap;
            public string? ColorHex;
            public string? SoundPath;
            // </Onyx-AdminAnnouncements>
        }
    }
}
