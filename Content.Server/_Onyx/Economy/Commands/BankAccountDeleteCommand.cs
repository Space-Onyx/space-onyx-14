using Content.Server._Onyx.Economy;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Commands;

[AdminCommand(AdminFlags.Admin)]
internal sealed class BankAccountDeleteCommand : IConsoleCommand
{
    public string Command => "bankaccountdelete";
    public string Description => "Удалить банковский аккаунт по его номеру: bankaccountdelete <account_number>";
    public string Help => "bankaccountdelete <account_number> -- Удаляет банковский аккаунт с указанным номером";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Использование: bankaccountdelete <account_number>");
            return;
        }

        if (!int.TryParse(args[0], out var accountNumber) || accountNumber < 1)
        {
            shell.WriteLine($"Неверный номер аккаунта: {args[0]}");
            return;
        }

        var entMan = IoCManager.Resolve<IEntityManager>();
        var bankCardSys = entMan.EntitySysManager.GetEntitySystem<BankCardSystem>();

        if (!bankCardSys.DeleteAccount(accountNumber))
        {
            shell.WriteLine($"Аккаунт с номером {accountNumber} не найден.");
            return;
        }

        shell.WriteLine($"Банковский аккаунт {accountNumber} успешно удалён.");
    }
}