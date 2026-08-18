using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Economy;

[Serializable, NetSerializable]
public sealed class TransactionRecord
{
    public enum TransactionType
    {
        Purchase,
        TransferSent,
        TransferReceived,
        Deposit,
        Withdraw
    }

    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string? CounterpartyAccount { get; set; }
    public string? CounterpartyName { get; set; }
    public string? Comment { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public TransactionRecord() {}
    public TransactionRecord(TransactionType type, string description, int amount, DateTime timestamp,
        string? counterpartyAccount = null, string? counterpartyName = null, string? comment = null)
    {
        Type = type;
        Description = description;
        Amount = amount;
        Timestamp = timestamp;
        CounterpartyAccount = counterpartyAccount;
        CounterpartyName = counterpartyName;
        Comment = comment;
    }
}
