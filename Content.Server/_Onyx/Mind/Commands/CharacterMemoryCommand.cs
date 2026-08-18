using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Server._Onyx.Mind.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class CharacterMemoryCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;

    public string Command => "character_memory";
    public string Description => "Управляет памятью персонажа.";
    public string Help => "character_memory <add|edit|remove> <entity_uid> <name> [value]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3 || args.Length > 4)
        {
            shell.WriteError($"Использование: {Help}");
            return;
        }

        var operation = args[0].ToLowerInvariant();
        if (operation is not ("add" or "edit" or "remove"))
        {
            shell.WriteError("Операция должна быть add, edit или remove.");
            return;
        }

        if (operation != "remove" && args.Length != 4 || operation == "remove" && args.Length != 3)
        {
            shell.WriteError($"Использование: {Help}");
            return;
        }

        if (!NetEntity.TryParse(args[1], out var netEntity) || !_entityManager.TryGetEntity(netEntity, out var entity) || entity is not { } entityUid)
        {
            shell.WriteError($"Не удалось найти сущность: {args[1]}");
            return;
        }

        if (string.IsNullOrWhiteSpace(args[2]))
        {
            shell.WriteError("Имя памяти не может быть пустым.");
            return;
        }

        var memoryName = args[2];
        if (!_entityManager.TryGetComponent<CharacterMemoryComponent>(entityUid, out var memoryComponent))
        {
            if (operation != "add")
            {
                shell.WriteError("У сущности нет памяти персонажа.");
                return;
            }

            memoryComponent = _entityManager.EnsureComponent<CharacterMemoryComponent>(entityUid);
        }

        var memory = memoryComponent.Memories.FirstOrDefault(x => x.Name == memoryName);
        switch (operation)
        {
            case "add":
                if (memory != null)
                {
                    shell.WriteError($"Память с именем '{memoryName}' уже существует.");
                    return;
                }

                memoryComponent.AddMemory(new Memory(memoryName, args[3]));
                shell.WriteLine($"Память '{memoryName}' добавлена.");
                break;

            case "edit":
                if (memory == null)
                {
                    shell.WriteError($"Память с именем '{memoryName}' не найдена.");
                    return;
                }

                memory.Value = args[3];
                shell.WriteLine($"Память '{memoryName}' изменена.");
                break;

            case "remove":
                if (memory == null)
                {
                    shell.WriteError($"Память с именем '{memoryName}' не найдена.");
                    return;
                }

                memoryComponent.Memories.Remove(memory);
                shell.WriteLine($"Память '{memoryName}' удалена.");
                break;
        }

        _entityManager.Dirty(entityUid, memoryComponent);
    }
}
