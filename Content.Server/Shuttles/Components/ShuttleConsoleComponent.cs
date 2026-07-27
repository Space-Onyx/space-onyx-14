using System.Numerics;
// <ShuttleSignalPorts>
using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;
// </ShuttleSignalPorts>
using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Components
{
    [RegisterComponent]
    public sealed partial class ShuttleConsoleComponent : SharedShuttleConsoleComponent
    {
        [ViewVariables]
        public readonly List<EntityUid> SubscribedPilots = new();

        /// <summary>
        /// How much should the pilot's eye be zoomed by when piloting using this console?
        /// </summary>
        [DataField("zoom")]
        public Vector2 Zoom = new(1.5f, 1.5f);

        /// <summary>
        /// Should this console have access to restricted FTL destinations?
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite), DataField("whitelistSpecific")]
        public List<EntityUid> FTLWhitelist = new List<EntityUid>();

        // <ShuttleSignalPorts>
        [DataField]
        public List<ProtoId<SourcePortPrototype>> SourcePorts =
        [
            "SignalShuttleConsole1",
            "SignalShuttleConsole2",
            "SignalShuttleConsole3",
            "SignalShuttleConsole4",
        ];
        // </ShuttleSignalPorts>
    }
}
