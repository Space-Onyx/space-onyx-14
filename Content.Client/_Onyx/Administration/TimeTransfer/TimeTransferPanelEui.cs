using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared._Onyx.Administration.TimeTransfer;
using JetBrains.Annotations;

namespace Content.Client._Onyx.Administration.TimeTransfer;

[UsedImplicitly]
public sealed class TimeTransferPanelEui : BaseEui
{
    private readonly TimeTransferPanel _panel = new();

    public TimeTransferPanelEui()
    {
        _panel.TransferRequested += args => SendMessage(new TimeTransferEuiMessage(args.PlayerId, args.Data, args.Overwrite));
    }

    public override void Opened() => _panel.OpenCentered();
    public override void Closed() => _panel.Close();

    public override void HandleState(EuiStateBase state)
    {
        if (state is TimeTransferPanelEuiState transfer)
            _panel.UpdatePermission(transfer.HasFlag);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is TimeTransferWarningEuiMessage warning)
            _panel.UpdateWarning(warning.Message, warning.WarningColor);
    }
}
