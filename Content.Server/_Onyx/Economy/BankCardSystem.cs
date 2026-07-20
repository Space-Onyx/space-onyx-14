using System;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Console;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Content.Server.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.CartridgeLoader;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Server.Station.Systems;
using Content.Shared._Onyx.Economy;
using Content.Shared.Access.Components;
using Content.Shared.Mind;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Economy;

public sealed partial class BankCardSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IdCardSystem _idCardSystem = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private BankCartridgeSystem _bankCartridge = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private int SalaryDelay => _cfg.GetCVar(CCVars.SalaryTime);

    private SalaryPrototype _salaries = default!;
    private readonly List<BankAccount> _accounts = new();
    private readonly Dictionary<int, BankAccount> _accountsById = new();
    private float _salaryTimer;

    [Dependency] private IConsoleHost _consoleHost = default!;

    public override void Initialize()
    {
        _salaries = _protoMan.Index<SalaryPrototype>(_cfg.GetCVar(CCVars.SalaryPrototypeId));

        if (!_consoleHost.AvailableCommands.ContainsKey("bankaccountcreate"))
            _consoleHost.RegisterCommand(new Content.Server.Commands.BankAccountCreateCommand());

        if (!_consoleHost.AvailableCommands.ContainsKey("bankaccountlist"))
            _consoleHost.RegisterCommand(new Content.Server.Commands.BankAccountListCommand());

        if (!_consoleHost.AvailableCommands.ContainsKey("bankaccountdelete"))
            _consoleHost.RegisterCommand(new Content.Server.Commands.BankAccountDeleteCommand());

        if (!_consoleHost.AvailableCommands.ContainsKey("bankaccountadjust"))
            _consoleHost.RegisterCommand(new Content.Server.Commands.BankAccountAdjustCommand());

        SubscribeLocalEvent<BankCardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            _salaryTimer = 0f;
            return;
        }

        _salaryTimer += frameTime;

        if (_salaryTimer <= SalaryDelay)
            return;

        _salaryTimer = 0f;
        PaySalary();
    }

    private void PaySalary()
    {
        if (!_cfg.GetCVar(CCVars.SalaryEnabled))
            return;

        var idCardQuery = EntityQueryEnumerator<IdCardComponent, BankCardComponent>();
        while (idCardQuery.MoveNext(out _, out var idCard, out var bankCard))
        {
            if (!bankCard.AccountId.HasValue || !TryGetAccount(bankCard.AccountId.Value, out var account))
                continue;

            if (!bankCard.IsPayrollEnabled)
                continue;

            if (account.Mind is not { Comp.UserId: not null, Comp.CurrentEntity: not null })
                continue;

            if (!_playerManager.TryGetSessionById(account.Mind.Value.Comp.UserId!.Value, out _) ||
                _mobState.IsDead(account.Mind.Value.Comp.CurrentEntity!.Value))
                continue;

            var salary = GetSalary(idCard);
            account.Balance += salary;
            account.AddTransaction(new TransactionRecord(
                TransactionRecord.TransactionType.Deposit,
                Loc.GetString("bank-program-ui-salary-description"),
                salary,
                Robust.Shared.Maths.Color.Lime,
                DateTime.MinValue.Add(_timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan))
            ));
        }

        _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("salary-pay-announcement"), Loc.GetString("salary-pay-sender"), colorOverride: Color.FromHex("#18abf5"));
    }

    private int GetSalary(IdCardComponent idCard)
    {
        var jobIcon = $"{idCard.JobIcon}";
        if (string.IsNullOrEmpty(jobIcon))
            return 0;
        var jobKey = jobIcon.StartsWith("JobIcon") ? jobIcon.Substring(7) : jobIcon;
        if (_salaries.Salaries.TryGetValue(jobKey, out var salary))
            return (int)(salary * _cfg.GetCVar(CCVars.SalaryMultiplier));
        return 0;
    }

    private void OnMapInit(EntityUid uid, BankCardComponent component, MapInitEvent args)
    {
        if (component.CommandBudgetCard &&
            TryComp(_station.GetOwningStation(uid), out Content.Shared.Cargo.Components.StationBankAccountComponent? stationBankAccount))
        {
            component.AccountId = 0;
            return;
        }

        if (component.AccountId.HasValue)
        {
            var acc = CreateAccount(component.AccountId.Value, component.StartingBalance);
            component.Pin = acc.AccountPin;
            return;
        }

        var account = CreateAccount(default, component.StartingBalance);
        component.AccountId = account.AccountId;
        component.Pin = account.AccountPin;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _accounts.Clear();
        _accountsById.Clear();
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (_idCardSystem.TryFindIdCard(ev.Mob, out var id) && TryComp<MindContainerComponent>(ev.Mob, out var mind))
        {
            var cardEntity = id.Owner;
            var bankCardComponent = EnsureComp<BankCardComponent>(cardEntity);

            if (!bankCardComponent.AccountId.HasValue || !TryGetAccount(bankCardComponent.AccountId.Value, out var bankAccount))
                return;

            // Sync PIN
            bankCardComponent.Pin = bankAccount.AccountPin;

            if (!TryComp(mind.Mind, out MindComponent? mindComponent))
                return;

            if (!TryComp<IdCardComponent>(id, out var idCardComp))
                return;

            bankAccount.Balance = GetSalary(idCardComp) + 100;
            var netEntity = GetNetEntity(ev.Mob);
            mindComponent.AddMemory(new Memory("PIN", bankAccount.AccountPin.ToString(), netEntity));
            mindComponent.AddMemory(new Memory(Loc.GetString("character-info-memories-account-number"),
                bankAccount.AccountId.ToString(), netEntity));
            bankAccount.Mind = (mind.Mind.Value, mindComponent);
            bankAccount.Name = Name(ev.Mob);

            if (!_inventorySystem.TryGetSlotEntity(ev.Mob, "id", out var pdaUid))
                return;

            BankCartridgeComponent? comp = null;

            var programs = _cartridgeLoader.GetInstalled(pdaUid.Value);
            EntityUid? program = null;
            foreach (var installedProgram in programs)
            {
                if (!TryComp(installedProgram, out comp))
                    continue;

                program = installedProgram;
                break;
            }

            if (comp == null)
                return;

            bankAccount.CartridgeUid = program;
            comp.AccountId = bankAccount.AccountId;
        }
    }

    public BankAccount CreateAccount(int accountId = default, int startingBalance = 0)
    {
        if (TryGetAccount(accountId, out var acc))
            return acc;

        BankAccount account;
        if (accountId == default)
        {
            int accountNumber;
            do
            {
                accountNumber = _random.Next(100000, 999999);
            } while (AccountExist(accountNumber));
            account = new BankAccount(accountNumber, startingBalance, _random);
        }
        else
        {
            account = new BankAccount(accountId, startingBalance, _random);
        }

        _accounts.Add(account);
        _accountsById[account.AccountId] = account;

        return account;
    }

    public bool AccountExist(int accountId)
    {
        return _accountsById.ContainsKey(accountId);
    }

    public bool TryGetAccount(int accountId, [NotNullWhen(true)] out BankAccount? account)
    {
        return _accountsById.TryGetValue(accountId, out account);
    }

    public int GetBalance(int accountId)
    {
        if (TryGetAccount(accountId, out var account))
        {
            return account.Balance;
        }

        return 0;
    }

    public bool TryChangeBalance(int accountId, int amount)
    {
        if (!TryGetAccount(accountId, out var account) || account.Balance + amount < 0)
            return false;

        if (account.CommandBudgetAccount)
        {
            while (AllEntityQuery<StationBankAccountComponent>().MoveNext(out var uid, out var stationBankAccount))
            {
                _cargo.UpdateBankAccount(new Entity<StationBankAccountComponent?>(uid, stationBankAccount), amount, stationBankAccount.PrimaryAccount);
                return true;
            }
        }

        account.Balance += amount;
        if (account.CartridgeUid != null)
            _bankCartridge.UpdateUiState(account.CartridgeUid.Value);

        return true;
    }

    public IReadOnlyList<BankAccount> GetAllAccounts()
    {
        return _accounts.AsReadOnly();
    }

    public bool DeleteAccount(int accountId)
    {
        if (!_accountsById.TryGetValue(accountId, out var account))
            return false;

        _accountsById.Remove(accountId);
        _accounts.Remove(account);
        return true;
    }

    public bool AdminChangeBalance(int accountId, int amount, string description)
    {
        if (!TryGetAccount(accountId, out var account))
            return false;

        if (!TryChangeBalance(accountId, amount))
            return false;

        var type = amount >= 0 ? TransactionRecord.TransactionType.Deposit : TransactionRecord.TransactionType.Withdraw;
        var color = amount >= 0 ? Robust.Shared.Maths.Color.Lime : Robust.Shared.Maths.Color.Red;
        account.AddTransaction(new TransactionRecord(
            type,
            description,
            amount,
            color,
            DateTime.MinValue.Add(_timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan))
        ));

        return true;
    }
}
