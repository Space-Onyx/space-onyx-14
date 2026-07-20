using Content.Shared.Cargo.Prototypes;
using Content.Shared.Mind;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Economy;

public sealed class BankAccount
{
    private const int MaxTransactions = 1000;
    private readonly TransactionRecord[] _transactions = new TransactionRecord[MaxTransactions];
    private int _transactionStart;
    private int _transactionCount;

    public void AddTransaction(TransactionRecord record)
    {
        if (_transactionCount < MaxTransactions)
        {
            var index = (_transactionStart + _transactionCount) % MaxTransactions;
            _transactions[index] = record;
            _transactionCount++;
            return;
        }

        _transactions[_transactionStart] = record;
        _transactionStart = (_transactionStart + 1) % MaxTransactions;
    }

    public List<TransactionRecord> GetTransactions(int count = 1000)
    {
        if (count <= 0)
            return new List<TransactionRecord>();

        if (count > MaxTransactions)
            count = MaxTransactions;

        if (_transactionCount == 0)
            return new List<TransactionRecord>();

        if (count > _transactionCount)
            count = _transactionCount;

        var result = new List<TransactionRecord>(count);
        for (var i = 0; i < count; i++)
        {
            var index = (_transactionStart + _transactionCount - 1 - i + MaxTransactions) % MaxTransactions;
            result.Add(_transactions[index]);
        }

        return result;
    }

    public readonly int AccountId;
    public int AccountPin;
    public int Balance;
    public bool CommandBudgetAccount;
    public Entity<MindComponent>? Mind;
    public string Name = string.Empty;
    public ProtoId<CargoAccountPrototype>? AccountPrototype;
    public EntityUid? CartridgeUid;

    public BankAccount(int accountId, int balance, IRobustRandom random)
    {
        AccountId = accountId;
        Balance = balance;
        AccountPin = random.Next(1000, 10000);
    }
}

