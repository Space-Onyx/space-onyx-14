using Content.Client.Eui;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map; // <Onyx-AdminAnnouncements>
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI
{
    public sealed class AdminAnnounceEui : BaseEui
    {
        private readonly AdminAnnounceWindow _window;

        public AdminAnnounceEui()
        {
            _window = new AdminAnnounceWindow();
            _window.OnClose += () => SendMessage(new CloseEuiMessage());
            _window.AnnounceButton.OnPressed += AnnounceButtonOnOnPressed;
        }

        // <Onyx-AdminAnnouncements>
        public override void HandleState(EuiStateBase state)
        {
            base.HandleState(state);
            if (state is not AdminAnnounceEuiState announceState)
                return;

            _window.SetStations(announceState.Stations);
            _window.SetMaps(announceState.Maps);
        }
        // </Onyx-AdminAnnouncements>

        private void AnnounceButtonOnOnPressed(BaseButton.ButtonEventArgs obj)
        {
            // <Onyx-AdminAnnouncements>
            NetEntity? selectedStation = null;
            if (_window.StationSelector.Visible && _window.StationSelector.SelectedId >= 0)
                selectedStation = (NetEntity?) _window.StationSelector.SelectedMetadata;

            MapId? selectedMap = null;
            if (_window.MapSelector.Visible && _window.MapSelector.SelectedId >= 0)
                selectedMap = (MapId?) _window.MapSelector.SelectedMetadata;
            // </Onyx-AdminAnnouncements>

            SendMessage(new AdminAnnounceEuiMsg.DoAnnounce
            {
                Announcement = Rope.Collapse(_window.Announcement.TextRope),
                Announcer =  _window.Announcer.Text,
                AnnounceType =  (AdminAnnounceType) (_window.AnnounceMethod.SelectedMetadata ?? AdminAnnounceType.AllStations), // <Onyx-AdminAnnouncements-edited>
                CloseAfter = !_window.KeepWindowOpen.Pressed,
                // <Onyx-AdminAnnouncements>
                SelectedStation = selectedStation,
                SelectedMap = selectedMap,
                ColorHex = _window.ColorInput.Text,
                SoundPath = _window.SoundInput.Text,
                // </Onyx-AdminAnnouncements>
            });

        }

        public override void Opened()
        {
            _window.OpenCentered();
        }

        public override void Closed()
        {
            _window.Close();
        }
    }
}
