using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Economy;
public enum ATMUiState
{
    NoCard,
    PinEntry,
    MainMenu
}

[Serializable, NetSerializable]
public sealed class ATMRequestWithdrawMessage : BoundUserInterfaceMessage
{
    public int Amount;
    public int Pin;

    public ATMRequestWithdrawMessage(int amount, int pin)
    {
        Amount = amount;
        Pin = pin;
    }
}

[Serializable, NetSerializable]
public sealed class ATMPinVerifyMessage : BoundUserInterfaceMessage
{
    public int Pin;

    public ATMPinVerifyMessage(int pin)
    {
        Pin = pin;
    }
}

[Serializable, NetSerializable]
public sealed class ATMPinVerifyResponseMessage : BoundUserInterfaceMessage
{
    public bool Success;
    public string OwnerName = string.Empty;

    public ATMPinVerifyResponseMessage(bool success, string ownerName)
    {
        Success = success;
        OwnerName = ownerName;
    }
}

[Serializable, NetSerializable]
public sealed class ATMBuiState : BoundUserInterfaceState
{
    public ATMUiState CurrentState;
    public string OwnerName = string.Empty;
    public int AccountBalance;
    public string ErrorMessage = string.Empty;
}
