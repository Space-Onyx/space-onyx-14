using Content.Server.Shuttles.Components;
using Content.Server._Onyx.ZLevels.Shuttles;
using Content.Shared._Onyx.ZLevels.Shuttles;

namespace Content.Server.Shuttles.Systems;

/// <summary>
/// Shuttle console handlers for z-level traversal (fly up / fly down). Kept here so the multiz
/// feature stays self-contained; the BUI subscriptions and state glue live in the main system.
/// </summary>
public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private CEZShuttleTraversalSystem _ztravel = default!;

    private void OnConsoleFlyUp(Entity<ShuttleConsoleComponent> ent, ref CEShuttleConsoleFlyUpMessage args)
        => TryConsoleTraversal(ent, 1);

    private void OnConsoleFlyDown(Entity<ShuttleConsoleComponent> ent, ref CEShuttleConsoleFlyDownMessage args)
        => TryConsoleTraversal(ent, -1);

    private void TryConsoleTraversal(Entity<ShuttleConsoleComponent> ent, int direction)
    {
        var consoleUid = GetDroneConsole(ent.Owner);
        if (consoleUid == null)
            return;

        var shuttleUid = Transform(consoleUid.Value).GridUid;
        if (shuttleUid == null)
            return;

        // Deck consoles resolve to the depth-0 leader that owns movement.
        var root = _ztravel.ResolveRoot(shuttleUid.Value);
        _ztravel.TryStartTraversal(root, direction);
    }
}
