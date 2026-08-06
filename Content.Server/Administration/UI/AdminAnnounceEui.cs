using Content.Server.Administration.Managers;
using Content.Server.Chat;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.Station.Systems; // <Onyx-AdminAnnouncements>
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Audio; // <Onyx-AdminAnnouncements>
using Robust.Shared.ContentPack; // <Onyx-AdminAnnouncements>
using Robust.Shared.Map; // <Onyx-AdminAnnouncements>
using Robust.Shared.Player; // <Onyx-AdminAnnouncements>
using Robust.Shared.Utility; // <Onyx-AdminAnnouncements>

namespace Content.Server.Administration.UI
{
    public sealed partial class AdminAnnounceEui : BaseEui
    {
        [Dependency] private IAdminManager _adminManager = default!;
        [Dependency] private IChatManager _chatManager = default!;
        // <Onyx-AdminAnnouncements>
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IResourceManager _resourceManager = default!;
        // </Onyx-AdminAnnouncements>
        private readonly ChatSystem _chatSystem;
        private readonly SharedMapSystem _mapManager; // <Onyx-AdminAnnouncements>
        private readonly StationSystem _stationSystem; // <Onyx-AdminAnnouncements>

        public AdminAnnounceEui()
        {
            IoCManager.InjectDependencies(this);
            _chatSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ChatSystem>();
            _mapManager = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SharedMapSystem>(); // <Onyx-AdminAnnouncements>
            _stationSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<StationSystem>(); // <Onyx-AdminAnnouncements>
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            // <Onyx-AdminAnnouncements-edited>
            var state = new AdminAnnounceEuiState();
            foreach (var (name, station) in _stationSystem.GetStationNames())
                state.Stations[station] = name;

            foreach (var map in _mapManager.GetAllMapIds())
                state.Maps[map] = $"Map {map}";

            return state;
            // </Onyx-AdminAnnouncements-edited>
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            switch (msg)
            {
                case AdminAnnounceEuiMsg.DoAnnounce doAnnounce:
                    if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
                    {
                        Close();
                        break;
                    }

                    // <Onyx-AdminAnnouncements>
                    var color = Color.Gold;
                    if (!string.IsNullOrWhiteSpace(doAnnounce.ColorHex) &&
                        Color.TryFromHex(doAnnounce.ColorHex.Trim(), out var parsedColor))
                    {
                        color = parsedColor;
                    }

                    SoundSpecifier? sound = null;
                    if (!string.IsNullOrWhiteSpace(doAnnounce.SoundPath))
                    {
                        var path = doAnnounce.SoundPath.Trim();
                        if (path.StartsWith('/') && ResPath.IsValidPath(path))
                        {
                            var resourcePath = new ResPath(path);
                            if (_resourceManager.ContentFileExists(resourcePath))
                                sound = new SoundPathSpecifier(resourcePath);
                        }
                    }
                    // </Onyx-AdminAnnouncements>

                    switch (doAnnounce.AnnounceType)
                    {
                        case AdminAnnounceType.Server:
                            _chatManager.DispatchServerAnnouncement(doAnnounce.Announcement, color); // <Onyx-AdminAnnouncements-edited>
                            break;
                        // <Onyx-AdminAnnouncements-edited>
                        case AdminAnnounceType.AllStations:
                            _chatSystem.DispatchGlobalAnnouncement(doAnnounce.Announcement,
                                doAnnounce.Announcer,
                                announcementSound: sound,
                                colorOverride: color);
                            break;
                        case AdminAnnounceType.SpecificStation:
                            if (doAnnounce.SelectedStation is { } selectedStation &&
                                _entityManager.TryGetEntity(selectedStation, out var station) &&
                                station.HasValue &&
                                _stationSystem.GetStationsSet().Contains(station.Value))
                            {
                                _chatSystem.DispatchStationAnnouncement(station.Value,
                                    doAnnounce.Announcement,
                                    doAnnounce.Announcer,
                                    announcementSound: sound,
                                    colorOverride: color);
                            }
                            break;
                        case AdminAnnounceType.SpecificMap:
                            if (doAnnounce.SelectedMap is { } selectedMap && _mapManager.MapExists(selectedMap))
                            {
                                var filter = Filter.Empty().AddWhereAttachedEntity(entity =>
                                    _entityManager.GetComponent<TransformComponent>(entity).MapID == selectedMap);
                                _chatSystem.DispatchFilteredAnnouncement(filter,
                                    doAnnounce.Announcement,
                                    sender: doAnnounce.Announcer,
                                    announcementSound: sound,
                                    colorOverride: color);
                            }
                            break;
                        // </Onyx-AdminAnnouncements-edited>
                    }

                    StateDirty();

                    if (doAnnounce.CloseAfter)
                        Close();

                    break;
            }
        }
    }
}
