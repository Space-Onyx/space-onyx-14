using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Economy;

[Serializable, NetSerializable]
public sealed class EftposBuiState : BoundUserInterfaceState
{
    public bool Locked;
    public int Amount;
    public string Owner = string.Empty;
    public bool ConfirmMode;
    public string PayerName = string.Empty;
}

[Serializable, NetSerializable]
public sealed class EftposLockMessage : BoundUserInterfaceMessage
{
    public int Amount;

    public EftposLockMessage(int amount)
    {
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class EftposConfirmMessage : BoundUserInterfaceMessage
{
    public int Pin;

    public EftposConfirmMessage(int pin)
    {
        Pin = pin;
    }
}

[Serializable, NetSerializable]
public sealed class EftposCancelMessage : BoundUserInterfaceMessage
{
}
