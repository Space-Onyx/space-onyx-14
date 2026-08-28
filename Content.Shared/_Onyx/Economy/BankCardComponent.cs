using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Roles;

namespace Content.Shared._Onyx.Economy;

[RegisterComponent, NetworkedComponent]
public sealed partial class BankCardComponent : Component, IEftposPinProvider
{
    [DataField]
    public int? AccountId;

    [DataField]
    public int StartingBalance;

    [DataField]
    public bool CommandBudgetCard;

    [DataField]
    public bool IsPayrollEnabled = true;

    [DataField]
    public ProtoId<JobPrototype>? PayrollJob;

    [DataField]
    public int? Pin;

    [DataField]
    public bool PINLocked = true;

    int? IEftposPinProvider.Pin => Pin;
}
