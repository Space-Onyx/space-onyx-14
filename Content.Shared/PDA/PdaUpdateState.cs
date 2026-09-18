using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared.PDA
{
    [Serializable, NetSerializable]
    public sealed class PdaUpdateState : CartridgeLoaderUiState // WTF is this. what. I ... fuck me I just want net entities to work
        // TODO purge this shit
        //AAAAAAAAAAAAAAAA
    {
        public bool FlashlightEnabled;
        public bool HasPen;
        public bool HasPai;
        public PdaIdInfoText PdaOwnerInfo;
        public string? StationName;
        public bool HasUplink;
        public bool CanPlayMusic;
        public string? Address;
        public Color ThemeAccent; // <Onyx-PdaTheme>
        public float BatteryCharge; // <Onyx-PdaBattery>
        public float BatteryMax; // <Onyx-PdaBattery>
        public float BatteryLowThreshold; // <Onyx-PdaBattery>
        public int DiskUsed; // <Onyx-PdaDisk>
        public int DiskMax; // <Onyx-PdaDisk>

        public PdaUpdateState(
            List<NetEntity> programs,
            NetEntity? activeUI,
            bool flashlightEnabled,
            bool hasPen,
            bool hasPai,
            PdaIdInfoText pdaOwnerInfo,
            string? stationName,
            bool hasUplink = false,
            bool canPlayMusic = false,
            string? address = null,
            float batteryCharge = 0f, // <Onyx-PdaBattery>
            float batteryMax = 0f, // <Onyx-PdaBattery>
            float batteryLowThreshold = 0.25f, // <Onyx-PdaBattery>
            int diskUsed = 0, // <Onyx-PdaDisk>
            int diskMax = 0) // <Onyx-PdaDisk>
            : base(programs, activeUI)
        {
            FlashlightEnabled = flashlightEnabled;
            HasPen = hasPen;
            HasPai = hasPai;
            PdaOwnerInfo = pdaOwnerInfo;
            HasUplink = hasUplink;
            CanPlayMusic = canPlayMusic;
            StationName = stationName;
            Address = address;
            ThemeAccent = Color.FromHex("#6B9A88"); // <Onyx-PdaTheme>
            BatteryCharge = batteryCharge; // <Onyx-PdaBattery>
            BatteryMax = batteryMax; // <Onyx-PdaBattery>
            BatteryLowThreshold = batteryLowThreshold; // <Onyx-PdaBattery>
            DiskUsed = diskUsed; // <Onyx-PdaDisk>
            DiskMax = diskMax; // <Onyx-PdaDisk>
        }
    }

    [Serializable, NetSerializable]
    public struct PdaIdInfoText
    {
        public string? ActualOwnerName;
        public string? IdOwner;
        public string? JobTitle;
        public string? StationAlertLevel;
        public Color StationAlertColor;
        public int MiningPoints; // <Onyx-MiningPointsPda>
        public uint BitrunningPoints; // <Onyx-PdaPoints>
    }
}
