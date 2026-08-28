using System.Linq;
using Content.Server.Administration;
using Content.Server.Access.Systems;
using Content.Shared._Onyx.Economy;
using Content.Shared.Administration;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Commands;

[AdminCommand(AdminFlags.Admin)]
internal sealed class SetMindJobCommand : IConsoleCommand
{
    public string Command => "setmindjob";
    public string Description => "Изменить должность на банковской карте игрока, используемую для начисления зарплаты";
    public string Help => "setmindjob <player> <job>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine($"Использование: {Help}");
            return;
        }

        var players = IoCManager.Resolve<IPlayerManager>();
        if (!players.TryGetSessionByUsername(args[0], out var session) || session.AttachedEntity is not { } mob)
        {
            shell.WriteError($"Не найден персонаж игрока {args[0]}.");
            return;
        }

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        if (!prototypes.HasIndex<JobPrototype>(args[1]))
        {
            shell.WriteError($"Должность {args[1]} не найдена.");
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        var idCards = entityManager.System<IdCardSystem>();
        if (!idCards.TryFindIdCard(mob, out var idCard) ||
            !entityManager.TryGetComponent(idCard.Owner, out BankCardComponent? bankCard))
        {
            shell.WriteError($"У игрока {args[0]} не найдена банковская ID-карта.");
            return;
        }

        bankCard.PayrollJob = args[1];
        shell.WriteLine($"Должность игрока {args[0]} изменена на {args[1]}.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                IoCManager.Resolve<IPlayerManager>().Sessions.Select(player => player.Name),
                "<player>"),
            2 => CompletionResult.FromHintOptions(
                IoCManager.Resolve<IPrototypeManager>().EnumeratePrototypes<JobPrototype>().Select(job => job.ID),
                "<job>"),
            _ => CompletionResult.Empty,
        };
    }
}
