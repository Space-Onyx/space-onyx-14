using Content.Server._Onyx.Economy;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Commands;

[AdminCommand(AdminFlags.Admin)]
internal sealed class BankAccountListCommand : IConsoleCommand
{
    public string Command => "bankaccountlist";
    public string Description => "Показать список всех банковских аккаунтов";
    public string Help => "bankaccountlist -- Выводит список всех банковских аккаунтов с номерами, PIN-кодами, балансами и владельцами";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine("Использование: bankaccountlist");
            return;
        }

        var entMan = IoCManager.Resolve<IEntityManager>();
        var bankCardSys = entMan.EntitySysManager.GetEntitySystem<BankCardSystem>();

        var accounts = bankCardSys.GetAllAccounts();

        if (accounts.Count == 0)
        {
            shell.WriteLine("Нет банковских аккаунтов.");
            return;
        }

        shell.WriteLine("Список банковских аккаунтов:");
        shell.WriteLine("Номер | PIN | Баланс | Владелец");
        shell.WriteLine("---------------------------------------");

        foreach (var account in accounts)
        {
            var pin = account.AccountPin.ToString("D4");
            var balance = account.Balance.ToString();
            var owner = string.IsNullOrWhiteSpace(account.Name) ? "Неизвестно" : account.Name;
            shell.WriteLine($"{account.AccountId} | {pin} | {balance} | {owner}");
        }
    }
}