using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared._Onyx.Economy;
using Content.Shared._Onyx.Time;
using Content.Shared.Access.Components;
using Content.Shared.PDA;
using Content.Shared.Chat;
using Robust.Shared.Utility;
using Robust.Shared.Maths;
using Content.Server.GameTicking;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;

namespace Content.Server._Onyx.Economy;

public sealed partial class BankCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoaderSystem = default!;
    [Dependency] private BankCardSystem _bankCardSystem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeAddedEvent>(OnInstall);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeRemovedEvent>(OnRemove);
    }

    private void OnRemove(EntityUid uid, BankCartridgeComponent component, CartridgeRemovedEvent args)
    {
        component.Loader = null;
    }

    private void OnInstall(EntityUid uid, BankCartridgeComponent component, CartridgeAddedEvent args)
    {
        component.Loader = args.Loader;
    }

    private void OnAccountLink(EntityUid uid, BankCartridgeComponent component, BankAccountLinkMessage args)
    {
        if (!_bankCardSystem.TryGetAccount(args.AccountId, out var account) || args.Pin != account.AccountPin ||
            account.CommandBudgetAccount)
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-link-error");
            return;
        }

        component.AccountLinkResult = Loc.GetString("bank-program-ui-link-success");

        if (args.AccountId != component.AccountId)
        {
            if (component.AccountId != null &&
                _bankCardSystem.TryGetAccount(component.AccountId.Value, out var oldAccount) &&
                oldAccount.CartridgeUid == uid)
                oldAccount.CartridgeUid = null;

            if (account.CartridgeUid != null)
                Comp<BankCartridgeComponent>(account.CartridgeUid.Value).AccountId = null;

            account.CartridgeUid = uid;
            component.AccountId = args.AccountId;
        }

        if (!TryComp(GetEntity(args.LoaderUid), out PdaComponent? pda) || !pda.ContainedId.HasValue)
            return;

        var bankCard = EnsureComp<BankCardComponent>(pda.ContainedId.Value);
        bankCard.AccountId = account.AccountId;
        bankCard.Pin = account.AccountPin;
        if (TryComp(pda.ContainedId.Value, out IdCardComponent? idCard) && idCard.JobPrototype != null)
            bankCard.PayrollJob = idCard.JobPrototype;
    }

    private void OnTransfer(EntityUid uid, BankCartridgeComponent component, BankTransferMessage args)
    {
        // Проверка: есть ли текущий аккаунт
        if (component.AccountId == null || !_bankCardSystem.TryGetAccount(component.AccountId.Value, out var fromAccount))
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-transfer-error-no-from");
            return;
        }
        // Проверка: нельзя переводить себе
        if (component.AccountId.Value == args.ToAccountId)
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-transfer-error-self");
            return;
        }
        // Проверка: есть ли целевой аккаунт
        if (!_bankCardSystem.TryGetAccount(args.ToAccountId, out var toAccount))
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-transfer-error-no-to");
            return;
        }
        // Проверка: сумма положительная
        if (args.Amount <= 0)
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-transfer-error-amount");
            return;
        }
        // Проверка: правильный ли PIN
        if (args.Pin != fromAccount.AccountPin)
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-transfer-error-pin");
            return;
        }
        // Проверка: хватает ли средств
        if (fromAccount.Balance < args.Amount)
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-transfer-error-nomoney");
            return;
        }
        // Перевод средств
        if (!_bankCardSystem.TryChangeBalance(fromAccount.AccountId, -args.Amount))
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-transfer-error-nomoney");
            return;
        }
        _bankCardSystem.TryChangeBalance(toAccount.AccountId, args.Amount);
        component.AccountLinkResult = Loc.GetString("bank-program-ui-transfer-success", ("to", toAccount.AccountId), ("amount", args.Amount));

        fromAccount.AddTransaction(new TransactionRecord(
            TransactionRecord.TransactionType.TransferSent,
            Loc.GetString("bank-program-ui-transaction-transfer-sent",
                ("account", toAccount.AccountId), ("name", toAccount.Name)),
            -args.Amount,
            InGameDate.Today(_configuration).Add(_timing.CurTime - _gameTicker.RoundStartTimeSpan),
            counterpartyAccount: toAccount.AccountId.ToString(),
            counterpartyName: toAccount.Name,
            comment: string.IsNullOrWhiteSpace(args.Comment) ? null : args.Comment
        ));
        toAccount.AddTransaction(new TransactionRecord(
            TransactionRecord.TransactionType.TransferReceived,
            Loc.GetString("bank-program-ui-transaction-transfer-received",
                ("account", fromAccount.AccountId), ("name", fromAccount.Name)),
            args.Amount,
            InGameDate.Today(_configuration).Add(_timing.CurTime - _gameTicker.RoundStartTimeSpan),
            counterpartyAccount: fromAccount.AccountId.ToString(),
            counterpartyName: fromAccount.Name,
            comment: string.IsNullOrWhiteSpace(args.Comment) ? null : args.Comment
        ));

        if (toAccount.CartridgeUid != null && Comp<Content.Shared.CartridgeLoader.CartridgeComponent>(toAccount.CartridgeUid.Value).LoaderUid is { } loaderUid)
        {
            var comment = string.IsNullOrWhiteSpace(args.Comment) ? string.Empty : args.Comment;
            _cartridgeLoaderSystem.SendNotification(
                loaderUid,
                "NanoBank",
                Loc.GetString("bank-program-ui-transfer-received", ("from", fromAccount.Name), ("amount", args.Amount), ("comment", comment))
            );
        }
    }

    private void OnChangePin(EntityUid uid, BankCartridgeComponent component, BankChangePinMessage args)
    {
        if (component.AccountId == null || !_bankCardSystem.TryGetAccount(component.AccountId.Value, out var account))
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-change-pin-error");
            return;
        }

        if (args.OldPin != account.AccountPin)
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-change-pin-wrong-old");
            return;
        }

        if (args.NewPin < 1000 || args.NewPin > 9999)
        {
            component.AccountLinkResult = Loc.GetString("bank-program-ui-change-pin-invalid");
            return;
        }

        account.AccountPin = args.NewPin;

        if (component.Loader != null && 
            TryComp(component.Loader.Value, out PdaComponent? pda) && 
            pda.ContainedId.HasValue && 
            TryComp(pda.ContainedId.Value, out BankCardComponent? bankCard))
        {
            bankCard.Pin = args.NewPin;
        }

        component.AccountLinkResult = Loc.GetString("bank-program-ui-change-pin-success");
    }

    private void OnUiReady(EntityUid uid, BankCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnUiMessage(EntityUid uid, BankCartridgeComponent component, CartridgeMessageEvent args)
    {
        switch (args)
        {
            case BankAccountLinkMessage message:
                OnAccountLink(uid, component, message);
                UpdateUiState(uid, GetEntity(args.LoaderUid), component);
                break;
            case BankChangePinMessage pinMessage:
                OnChangePin(uid, component, pinMessage);
                UpdateUiState(uid, GetEntity(args.LoaderUid), component);
                break;
            case BankTransferMessage transferMessage:
                OnTransfer(uid, component, transferMessage);
                UpdateUiState(uid, GetEntity(args.LoaderUid), component);
                break;
            case BankTransactionHistoryRequestMessage msg:
                OnTransactionHistoryRequest(uid, component, msg, GetEntity(args.LoaderUid));
                break;
            case CartridgeUiRefreshMessage:
                UpdateUiState(uid, GetEntity(args.LoaderUid), component);
                break;
        }
    }

    private void UpdateUiState(EntityUid cartridgeUid, EntityUid loaderUid, BankCartridgeComponent? component = null)
    {
        if (!Resolve(cartridgeUid, ref component))
            return;

        var accountLinkMessage = Loc.GetString("bank-program-ui-link-program") + '\n';
        if (TryComp(loaderUid, out PdaComponent? pda) && pda.ContainedId.HasValue)
        {
            accountLinkMessage += TryComp(pda.ContainedId.Value, out BankCardComponent? bankCard)
                ? Loc.GetString("bank-program-ui-link-id-card-linked", ("account", bankCard.AccountId!.Value))
                : Loc.GetString("bank-program-ui-link-id-card");
        }
        else
        {
            accountLinkMessage += Loc.GetString("bank-program-ui-link-no-id-card");
        }

        var state = new BankCartridgeUiState
        {
            AccountLinkResult = component.AccountLinkResult,
            AccountLinkMessage = accountLinkMessage
        };

        if (component.AccountId != null && _bankCardSystem.TryGetAccount(component.AccountId.Value, out var account))
        {
            state.Balance = account.Balance;
            state.AccountId = account.AccountId;
            state.OwnerName = account.Name;
        }
        _cartridgeLoaderSystem.UpdateCartridgeUiState(loaderUid, state);
    }

    public void UpdateUiState(EntityUid cartridgeUid)
    {
        if (!TryComp(cartridgeUid, out BankCartridgeComponent? component) || component.Loader == null)
            return;

        UpdateUiState(cartridgeUid, component.Loader.Value, component);
    }

    private void OnTransactionHistoryRequest(EntityUid uid, BankCartridgeComponent component, BankTransactionHistoryRequestMessage msg, EntityUid loaderUid)
    {
        if (!_bankCardSystem.TryGetAccount(msg.AccountId, out var account))
        {
            return;
        }
        var records = account.GetTransactions(msg.Count);
        _cartridgeLoaderSystem.UpdateCartridgeUiState(loaderUid, new BankTransactionHistoryResponseMessage(records));
    }
}
