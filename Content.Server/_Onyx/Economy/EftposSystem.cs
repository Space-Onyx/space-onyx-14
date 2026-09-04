using Content.Server.Hands.Systems;
using Content.Shared._Onyx.Economy;
using Content.Shared._Onyx.Time;
using Content.Shared.Access.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Content.Server.GameTicking;
using Robust.Shared.Timing;
using Content.Shared.Inventory;
using Robust.Shared.Configuration;

namespace Content.Server._Onyx.Economy;

public sealed partial class EftposSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private BankCardSystem _bankCardSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private HandsSystem _sharedHandsSystem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    private readonly HashSet<EntityUid> _pendingTimeouts = new();
    private readonly List<EntityUid> _pendingTimeoutBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EftposComponent, EftposLockMessage>(OnLock);
        SubscribeLocalEvent<EftposComponent, EftposConfirmMessage>(OnConfirm);
        SubscribeLocalEvent<EftposComponent, EftposCancelMessage>(OnCancel);
        SubscribeLocalEvent<EftposComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<EftposComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingTimeouts.Count == 0)
            return;

        _pendingTimeoutBuffer.Clear();
        foreach (var uid in _pendingTimeouts)
        {
            _pendingTimeoutBuffer.Add(uid);
        }

        var now = _timing.CurTime;
        foreach (var uid in _pendingTimeoutBuffer)
        {
            if (!TryComp<EftposComponent>(uid, out var component))
            {
                _pendingTimeouts.Remove(uid);
                continue;
            }

            if (component.PendingTimeout is not { } timeout)
            {
                _pendingTimeouts.Remove(uid);
                continue;
            }

            if (now >= timeout)
            {
                // Timeout expired, cancel pending payment
                ClearPending(uid, component);
                UpdateUiState(uid, component.BankAccountId != null, component.Amount,
                    GetOwner(EntityUid.Invalid, component.BankAccountId), false, string.Empty);
                // Close UI on timeout
                _ui.CloseUis(uid);
            }
        }
    }

    private void OnInteractUsing(EntityUid uid, EftposComponent component, InteractUsingEvent args)
    {
        if (component.BankAccountId == null
            || !TryComp(args.Used, out BankCardComponent? bankCard)
            || bankCard.AccountId == component.BankAccountId
            || component.Amount <= 0
            || bankCard.CommandBudgetCard)
            return;

        // If there's already a pending payment, reject to avoid conflicts
        if (component.PendingPayerAccountId != null)
        {
            _popupSystem.PopupEntity(Loc.GetString("eftpos-already-pending"), uid);
            return;
        }

        // Close any existing UI for other players before initiating a new transaction
        CloseExistingUi(uid, component);

        // Set pending payment and open UI for confirmation
        component.PendingPayerAccountId = bankCard.AccountId;

        // Get payer name from bank account first, fallback to ID card
        component.PendingPayerName = GetPayerName(args.User, bankCard);

        // Get PIN from bank card or account
        if (!_bankCardSystem.TryGetAccount(bankCard.AccountId!.Value, out var account))
            return;

        component.PendingPin = bankCard.Pin ?? account.AccountPin;
        component.PendingTimeout = _timing.CurTime + TimeSpan.FromMinutes(2);
        _pendingTimeouts.Add(uid);

        UpdateUiState(uid, component.BankAccountId != null, component.Amount,
            GetOwner(EntityUid.Invalid, component.BankAccountId), true, component.PendingPayerName);

        // Open UI for the user
        _ui.TryOpenUi(uid, EftposKey.Key, args.User);
    }

    private void OnConfirm(EntityUid uid, EftposComponent component, EftposConfirmMessage args)
    {
        if (component.PendingPayerAccountId == null || component.BankAccountId == null)
            return;

        // Check PIN
        if (component.PendingPin != null && component.PendingPin != args.Pin)
        {
            _popupSystem.PopupEntity(Loc.GetString("eftpos-wrong-pin"), uid);
            _audioSystem.PlayPvs(component.SoundDeny, uid);
            ClearPending(uid, component);
            return;
        }

        // Process the transaction
        if (_bankCardSystem.TryChangeBalance(component.PendingPayerAccountId.Value, -component.Amount) &&
            _bankCardSystem.TryChangeBalance(component.BankAccountId.Value, component.Amount))
        {
            _popupSystem.PopupEntity(Loc.GetString("eftpos-transaction-success"), uid);
            _audioSystem.PlayPvs(component.SoundApply, uid);

            // Add transactions
            if (_bankCardSystem.TryGetAccount(component.PendingPayerAccountId.Value, out var payerAccount))
            {
                string receiverName = component.BankAccountId.Value.ToString();
                if (_bankCardSystem.TryGetAccount(component.BankAccountId.Value, out var receiverAcc))
                    receiverName = string.IsNullOrWhiteSpace(receiverAcc.Name) ? receiverAcc.AccountId.ToString() : receiverAcc.Name;
                payerAccount.AddTransaction(new TransactionRecord(
                    TransactionRecord.TransactionType.Purchase,
                    Loc.GetString("bank-program-ui-transaction-purchase-sent",
                        ("account", component.BankAccountId.Value), ("name", receiverName)),
                    -component.Amount,
                    InGameDate.Today(_configuration).Add(_timing.CurTime - _gameTicker.RoundStartTimeSpan)
                ));
            }
            if (_bankCardSystem.TryGetAccount(component.BankAccountId.Value, out var receiverAccount))
            {
                string payerName = component.PendingPayerName;
                receiverAccount.AddTransaction(new TransactionRecord(
                    TransactionRecord.TransactionType.Purchase,
                    Loc.GetString("bank-program-ui-transaction-purchase-received",
                        ("account", component.PendingPayerAccountId.Value), ("name", payerName)),
                    component.Amount,
                    InGameDate.Today(_configuration).Add(_timing.CurTime - _gameTicker.RoundStartTimeSpan)
                ));
            }
        }
        else
        {
            _popupSystem.PopupEntity(Loc.GetString("eftpos-transaction-error"), uid);
            _audioSystem.PlayPvs(component.SoundDeny, uid);
        }

        ClearPending(uid, component);
        // Close UI after transaction attempt
        _ui.CloseUi(uid, EftposKey.Key, args.Actor);
    }

    private void OnCancel(EntityUid uid, EftposComponent component, EftposCancelMessage args)
    {
        ClearPending(uid, component);
        UpdateUiState(uid, component.BankAccountId != null, component.Amount,
            GetOwner(EntityUid.Invalid, component.BankAccountId), false, string.Empty);
        // Close UI on cancel
        _ui.CloseUi(uid, EftposKey.Key, args.Actor);
    }

    private void ClearPending(EntityUid uid, EftposComponent component)
    {
        component.PendingPayerAccountId = null;
        component.PendingPayerName = string.Empty;
        component.PendingPin = null;
        component.PendingTimeout = null;
        _pendingTimeouts.Remove(uid);
    }

    private void OnShutdown(EntityUid uid, EftposComponent component, ComponentShutdown args)
    {
        _pendingTimeouts.Remove(uid);
    }

    private void OnLock(EntityUid uid, EftposComponent component, EftposLockMessage args)
    {
        if (!TryComp(args.Actor, out HandsComponent? hands))
            return;
        var activeHandId = hands.ActiveHandId;
        if (activeHandId == null)
            return;
        if (!_sharedHandsSystem.TryGetHeldItem((args.Actor, hands), activeHandId, out var activeEntity))
            return;
        if (!TryComp(activeEntity, out BankCardComponent? bankCard))
            return;

        if (component.BankAccountId == null)
        {
            component.BankAccountId = bankCard.AccountId;
            component.Amount = args.Amount;
        }
        else if (component.BankAccountId == bankCard.AccountId)
        {
            component.BankAccountId = null;
            component.Amount = 0;
        }
        else
        {
            // Not the owner trying to unlock
            _popupSystem.PopupEntity(Loc.GetString("eftpos-not-owner"), args.Actor);
            _ui.CloseUi(uid, EftposKey.Key, args.Actor);
            return;
        }

        ClearPending(uid, component); // Clear any pending on lock/unlock

        // Close any existing UI for other players before opening for the new user
        CloseExistingUi(uid, component);

        EntityUid? ownerEntity = null;
        if (activeHandId != null && _sharedHandsSystem.TryGetHeldItem((args.Actor, hands), activeHandId, out var ent))
            ownerEntity = ent;
        UpdateUiState(uid, component.BankAccountId != null, component.Amount,
            GetOwner(ownerEntity ?? EntityUid.Invalid, component.BankAccountId), false, string.Empty);
    }

    private void CloseExistingUi(EntityUid uid, EftposComponent component)
    {
        // Close any existing UI sessions for this EFTPOS device
        _ui.CloseUis(uid);
    }

    private string GetPayerName(EntityUid user, BankCardComponent? bankCard = null)
    {
        // Try to get bank card from hands first
        if (bankCard != null && bankCard.AccountId.HasValue)
        {
            if (_bankCardSystem.TryGetAccount(bankCard.AccountId.Value, out var account) &&
                !string.IsNullOrWhiteSpace(account.Name))
            {
                return account.Name;
            }
        }

        return "????";
    }

    private string GetOwner(EntityUid uid, int? bankAccountId)
    {
        if (bankAccountId == null || !_bankCardSystem.TryGetAccount(bankAccountId.Value, out var account))
            return string.Empty;

        if (TryComp(uid, out IdCardComponent? idCard) && idCard.FullName != null)
            return idCard.FullName;

        return account.Name == string.Empty ? account.AccountId.ToString() : account.Name;
    }

    private void UpdateUiState(EntityUid uid, bool locked, int amount, string owner, bool confirmMode, string payerName)
    {
        var state = new EftposBuiState
        {
            Locked = locked,
            Amount = amount,
            Owner = owner,
            ConfirmMode = confirmMode,
            PayerName = payerName
        };

        _ui.SetUiState(uid, EftposKey.Key, state);
    }
}
