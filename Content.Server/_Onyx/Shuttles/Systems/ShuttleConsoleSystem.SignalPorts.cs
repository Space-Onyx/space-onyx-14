using System.Linq;
using Content.Server.Shuttles.Components;
using Content.Shared._Onyx.Shuttles.Events;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    private void OnConsoleStartup(Entity<ShuttleConsoleComponent> console, ref ComponentStartup args)
    {
        _deviceLink.EnsureSourcePorts(console, console.Comp.SourcePorts.ToArray());
    }

    private void OnShuttlePortButtonPressed(
        Entity<ShuttleConsoleComponent> console,
        ref ShuttlePortButtonPressedMessage args)
    {
        var sourcePort = args.SourcePort;
        if (!console.Comp.SourcePorts.Any(port => port.Id == sourcePort))
            return;

        _deviceLink.SendSignal(console, sourcePort, true);
    }
}
