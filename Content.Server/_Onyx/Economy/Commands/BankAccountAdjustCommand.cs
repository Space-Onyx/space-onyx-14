using System.Linq;
using Content.Server._Onyx.Economy;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Commands;

[AdminCommand(AdminFlags.Admin)]
internal sealed class BankAccountAdjustCommand : IConsoleCommand
{
    public string Command => "bankaccountadjust";
    public string Description => "Изменить баланс банковского аккаунта: bankaccountadjust <account_number> <amount> <description>";
    public string Help => "bankaccountadjust <account_number> <amount> <description> -- Изменяет баланс аккаунта на указанную сумму (положительная - добавить, отрицательная - снять). Добавляет запись в историю транзакций с указанным описанием";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteLine("Использование: bankaccountadjust <account_number> <amount> <description>");
            return;
        }

        if (!int.TryParse(args[0], out var accountNumber) || accountNumber < 1)
        {
            shell.WriteLine($"Неверный номер аккаунта: {args[0]}");
            return;
        }

        if (!int.TryParse(args[1], out var amount))
        {
            shell.WriteLine($"Неверная сумма: {args[1]}");
            return;
        }

        var description = string.Join(" ", args.Skip(2));

        var entMan = IoCManager.Resolve<IEntityManager>();
        var bankCardSys = entMan.EntitySysManager.GetEntitySystem<BankCardSystem>();

        if (!bankCardSys.AdminChangeBalance(accountNumber, amount, description))
        {
            shell.WriteLine($"Не удалось изменить баланс аккаунта {accountNumber}. Возможно, аккаунт не найден или недостаточно средств для снятия.");
            return;
        }

        shell.WriteLine($"Баланс аккаунта {accountNumber} изменён на {amount}.");
    }
}