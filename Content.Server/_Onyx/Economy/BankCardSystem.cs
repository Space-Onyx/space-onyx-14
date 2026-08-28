using System;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Console;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Content.Server.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Shared.CartridgeLoader;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Onyx.Economy;
using Content.Shared.Access.Components;
using Content.Shared.Mind;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Economy;

public sealed partial class BankCardSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IdCardSystem _idCardSystem = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
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

        if (!_consoleHost.AvailableCommands.ContainsKey("setmindjob"))
            _consoleHost.RegisterCommand(new Content.Server.Commands.SetMindJobCommand());

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

        var salaryDelay = Math.Max(1, SalaryDelay);
        while (_salaryTimer >= salaryDelay)
        {
            _salaryTimer -= salaryDelay;
            PaySalary();
        }
    }

    private void PaySalary()
    {
        if (!_cfg.GetCVar(CCVars.SalaryEnabled))
            return;

        var paidAccounts = new HashSet<int>();
        var idCardQuery = EntityQueryEnumerator<IdCardComponent, BankCardComponent>();
        while (idCardQuery.MoveNext(out _, out _, out var bankCard))
        {
            if (!bankCard.AccountId.HasValue || !TryGetAccount(bankCard.AccountId.Value, out var account))
                continue;

            if (!bankCard.IsPayrollEnabled)
                continue;

            var salary = GetSalary(bankCard.PayrollJob);
            if (salary == null || !paidAccounts.Add(account.AccountId))
                continue;

            account.Balance += salary.Value;
            account.AddTransaction(new TransactionRecord(
                TransactionRecord.TransactionType.Deposit,
                Loc.GetString("bank-program-ui-salary-description"),
                salary.Value,
                DateTime.Now.Date.AddYears(1000).Add(_timing.CurTime - _gameTicker.RoundStartTimeSpan)
            ));
        }

        foreach (var station in _station.GetStationsSet())
            _chatSystem.DispatchStationAnnouncement(station, Loc.GetString("salary-pay-announcement"), Loc.GetString("salary-pay-sender"), colorOverride: Color.FromHex("#18abf5"));
    }

    private int? GetSalary(ProtoId<JobPrototype>? job)
    {
        if (job == null || !_salaries.Salaries.TryGetValue(job.Value.Id, out var salary))
            return null;

        return (int)(salary * _cfg.GetCVar(CCVars.SalaryMultiplier));
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
            bankCardComponent.PayrollJob = ev.JobId;

            if (!TryComp(mind.Mind, out MindComponent? mindComponent))
                return;

            bankAccount.Balance = (GetSalary(bankCardComponent.PayrollJob) ?? 0) + 100;
            var netEntity = GetNetEntity(ev.Mob);
            var memory = EnsureComp<CharacterMemoryComponent>(ev.Mob);
            memory.AddMemory(new Memory("PIN", bankAccount.AccountPin.ToString(), netEntity));
            memory.AddMemory(new Memory(Loc.GetString("character-info-memories-account-number"),
                bankAccount.AccountId.ToString(), netEntity));
            bankAccount.Mind = (mind.Mind.Value, mindComponent);
            bankAccount.Name = Name(ev.Mob);

            if (!_inventorySystem.TryGetSlotEntity(ev.Mob, "id", out var pdaUid))
                return;

            var bankProgram = _cartridgeLoader.TryGetProgram<BankCartridgeComponent>(pdaUid.Value);
            if (bankProgram is not { } program)
                return;

            bankAccount.CartridgeUid = program.Owner;
            program.Comp.AccountId = bankAccount.AccountId;
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
        account.AddTransaction(new TransactionRecord(
            type,
            description,
            amount,
            DateTime.Now.Date.AddYears(1000).Add(_timing.CurTime - _gameTicker.RoundStartTimeSpan)
        ));

        return true;
    }
}
