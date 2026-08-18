using System;
using System.Linq;
using Content.Server.Stack;
using Content.Server.Store.Components;
using Content.Shared.Store.Components;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared._Onyx.Economy;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Economy;

public sealed partial class ATMSystem : SharedATMSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private BankCardSystem _bankCardSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private StackSystem _stackSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    private Dictionary<EntityUid, EntityUid> _authenticatedCard = new();
    private readonly HashSet<EntityUid> _emaggedAtms = new();
    private readonly List<EntityUid> _emaggedAtmBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ATMComponent, EntInsertedIntoContainerMessage>(OnCardInserted);
        SubscribeLocalEvent<ATMComponent, EntRemovedFromContainerMessage>(OnCardRemoved);
        SubscribeLocalEvent<ATMComponent, ATMRequestWithdrawMessage>(OnWithdrawRequest);
        SubscribeLocalEvent<ATMComponent, ATMPinVerifyMessage>(OnPinVerify);
        SubscribeLocalEvent<ATMComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ATMComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<ATMComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<ATMComponent, GotEmaggedEvent>(OnEmag);
    }

    private void OnEmag(EntityUid uid, ATMComponent component, ref GotEmaggedEvent args)
    {
        component.EmaggedUntil = _timing.CurTime + TimeSpan.FromSeconds(component.EmagDuration);
        _emaggedAtms.Add(uid);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_emaggedAtms.Count == 0)
            return;

        _emaggedAtmBuffer.Clear();
        foreach (var uid in _emaggedAtms)
        {
            _emaggedAtmBuffer.Add(uid);
        }

        var now = _timing.CurTime;
        foreach (var uid in _emaggedAtmBuffer)
        {
            if (!TryComp<ATMComponent>(uid, out var component))
            {
                _emaggedAtms.Remove(uid);
                continue;
            }

            if (component.EmaggedUntil is not { } until || until > now)
                continue;

            component.EmaggedUntil = null;
            _emaggedAtms.Remove(uid);
            _authenticatedCard.Remove(uid);

            if (HasComp<EmaggedComponent>(uid))
                RemComp<EmaggedComponent>(uid);

            if (component.CardSlot.Item.HasValue)
                UpdateUiState(uid, ATMUiState.PinEntry, string.Empty, 0);
            else
                UpdateUiState(uid, ATMUiState.NoCard, string.Empty, 0);
        }
    }

    private void OnComponentStartup(EntityUid uid, ATMComponent component, ComponentStartup args)
    {
        if (component.EmaggedUntil.HasValue)
            _emaggedAtms.Add(uid);

        UpdateUiState(uid, ATMUiState.NoCard, string.Empty, 0);
    }

    private void OnComponentShutdown(EntityUid uid, ATMComponent component, ComponentShutdown args)
    {
        _emaggedAtms.Remove(uid);
        _authenticatedCard.Remove(uid);
    }

    private void OnInteractUsing(EntityUid uid, ATMComponent component, InteractUsingEvent args)
    {
        if (!TryComp<CurrencyComponent>(args.Used, out var currency) || !currency.Price.Keys.Contains(component.CurrencyType))
        {
            return;
        }

        if (!component.CardSlot.Item.HasValue)
        {
            _popupSystem.PopupEntity(Loc.GetString("atm-trying-insert-cash-error"), args.Target, args.User, PopupType.Medium);
            _audioSystem.PlayPvs(component.SoundDeny, uid);
            return;
        }

        var stack = Comp<StackComponent>(args.Used);
        var bankCard = Comp<BankCardComponent>(component.CardSlot.Item.Value);
        var amount = stack.Count;

        if (_bankCardSystem.TryChangeBalance(bankCard.AccountId!.Value, amount))
        {
            Del(args.Used);
            args.Handled = true;
            var newBalance = _bankCardSystem.GetBalance(bankCard.AccountId.Value);

            if (_authenticatedCard.TryGetValue(uid, out var authCard) && authCard == component.CardSlot.Item.Value)
            {
                if (_bankCardSystem.TryGetAccount(bankCard.AccountId.Value, out var account))
                {
                    UpdateUiState(uid, ATMUiState.MainMenu, account.Name, newBalance);
                }
            }

            _audioSystem.PlayPvs(component.SoundInsertCurrency, uid);
            if (_bankCardSystem.TryGetAccount(bankCard.AccountId.Value, out var acc))
            {
                acc.AddTransaction(new TransactionRecord(
                    TransactionRecord.TransactionType.Deposit,
                    Loc.GetString("bank-program-ui-transaction-deposit-atm"),
                    amount,
                    DateTime.Now.Date.Add(_timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan))
                ));
            }
        }
    }

    private void OnCardInserted(EntityUid uid, ATMComponent component, EntInsertedIntoContainerMessage args)
    {
        if (component.CardSlot.ContainerSlot != args.Container)
            return;

        if (!TryComp<BankCardComponent>(args.Entity, out var bankCard) || !bankCard.AccountId.HasValue)
        {
            _container.EmptyContainer(args.Container);
            return;
        }

        if (IsEmagActive(component))
        {
            _authenticatedCard[uid] = args.Entity;
            var balance = _bankCardSystem.GetBalance(bankCard.AccountId.Value);
            if (_bankCardSystem.TryGetAccount(bankCard.AccountId.Value, out var account))
            {
                UpdateUiState(uid, ATMUiState.MainMenu, account.Name, balance);
            }
            return;
        }

        UpdateUiState(uid, ATMUiState.PinEntry, string.Empty, 0);
    }

    private void OnCardRemoved(EntityUid uid, ATMComponent component, EntRemovedFromContainerMessage args)
    {
        if (component.CardSlot.ContainerSlot != args.Container)
            return;

        _authenticatedCard.Remove(uid);
        UpdateUiState(uid, ATMUiState.NoCard, string.Empty, 0);
    }

    private void OnPinVerify(EntityUid uid, ATMComponent component, ATMPinVerifyMessage args)
    {
        if (!TryComp<BankCardComponent>(component.CardSlot.Item, out var bankCard) || !bankCard.AccountId.HasValue)
        {
            if (component.CardSlot.ContainerSlot != null)
                _container.EmptyContainer(component.CardSlot.ContainerSlot);
            return;
        }

        if (!_bankCardSystem.TryGetAccount(bankCard.AccountId.Value, out var account) ||
            ((bankCard.Pin ?? account.AccountPin) != args.Pin && !IsEmagActive(component)))
        {
            _popupSystem.PopupEntity(Loc.GetString("atm-wrong-pin"), uid);
            _audioSystem.PlayPvs(component.SoundDeny, uid);
            return;
        }

        _authenticatedCard[uid] = component.CardSlot.Item.Value;
        var balance = _bankCardSystem.GetBalance(bankCard.AccountId.Value);
        UpdateUiState(uid, ATMUiState.MainMenu, account.Name, balance);
        _audioSystem.PlayPvs(component.SoundApply, uid);
    }

    private void OnWithdrawRequest(EntityUid uid, ATMComponent component, ATMRequestWithdrawMessage args)
    {
        if (!TryComp<BankCardComponent>(component.CardSlot.Item, out var bankCard) || !bankCard.AccountId.HasValue ||
            !_authenticatedCard.TryGetValue(uid, out var authCard) || authCard != component.CardSlot.Item.Value)
        {
            _authenticatedCard.Remove(uid);
            UpdateUiState(uid, ATMUiState.PinEntry, string.Empty, 0);
            return;
        }

        if (!_bankCardSystem.TryChangeBalance(bankCard.AccountId.Value, -args.Amount))
        {
            _popupSystem.PopupEntity(Loc.GetString("atm-not-enough-cash"), uid);
            _audioSystem.PlayPvs(component.SoundDeny, uid);
            return;
        }

        if (_bankCardSystem.TryGetAccount(bankCard.AccountId.Value, out var account))
        {
            account.AddTransaction(new TransactionRecord(
                TransactionRecord.TransactionType.Withdraw,
                    Loc.GetString("bank-program-ui-transaction-withdraw-atm"),
                -args.Amount,
                    DateTime.Now.Date.Add(_timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan))
            ));
        }

        var transform = Transform(uid);
        var forward = transform.LocalRotation.ToWorldVec();
        var offset = forward * 0.7f;
        var spawnCoords = transform.Coordinates.Offset(offset);
        _stackSystem.SpawnAtPosition(args.Amount, _prototypeManager.Index<StackPrototype>(component.CreditStackPrototype), spawnCoords);
        _audioSystem.PlayPvs(component.SoundWithdrawCurrency, uid);

        // Update UI with new balance
        var newBalance = _bankCardSystem.GetBalance(bankCard.AccountId.Value);
        UpdateUiState(uid, ATMUiState.MainMenu, account?.Name ?? string.Empty, newBalance);
    }

    private void UpdateUiState(EntityUid uid, ATMUiState state, string ownerName, int balance)
    {
        var stateObj = new ATMBuiState
        {
            CurrentState = state,
            OwnerName = ownerName,
            AccountBalance = balance
        };

        _ui.SetUiState(uid, ATMUiKey.Key, stateObj);
    }

    private bool IsEmagActive(ATMComponent component)
    {
        return component.EmaggedUntil is { } until && until > _timing.CurTime;
    }
}
