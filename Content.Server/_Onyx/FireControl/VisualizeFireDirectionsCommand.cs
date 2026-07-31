using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Onyx.FireControl;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class VisualizeFireDirectionsCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IEntitySystemManager _systemManager = default!;

    public string Command => "visualizefire";
    public string Description => "Toggles visualization of firing directions for a FireControllable entity";
    public string Help => "visualizefire <entity id>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError("Expected an entity ID argument.");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var entityUid))
        {
            shell.WriteError($"Failed to parse entity ID '{args[0]}'.");
            return;
        }

        if (!_entityManager.HasComponent<FireControllableComponent>(entityUid))
        {
            shell.WriteError($"Entity {entityUid} does not have a FireControllable component.");
            return;
        }

        var enabled = _systemManager.GetEntitySystem<FireControlSystem>().ToggleVisualization(entityUid);
        shell.WriteLine(enabled
            ? $"Visualization enabled for entity {entityUid}."
            : $"Visualization disabled for entity {entityUid}.");
    }
}
