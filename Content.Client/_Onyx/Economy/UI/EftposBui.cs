using Content.Shared._Onyx.Economy;
using JetBrains.Annotations;

namespace Content.Client._Onyx.Economy.UI;

[UsedImplicitly]
public sealed class EftposBui : BoundUserInterface
{
    private readonly EftposWindow _window;

    public EftposBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _window = new EftposWindow();
    }

    protected override void Open()
    {
        base.Open();
        _window.OnClose += OnWindowClosed;
        _window.OnCardButtonPressed += SendMessage;
        _window.OnConfirmButtonPressed += SendMessage;
        _window.OnCancelButtonPressed += () => SendMessage(new EftposCancelMessage());

        if (State != null)
        {
            UpdateState(State);
        }

        _window.OpenCentered();
    }

    private void OnWindowClosed()
    {
        // If in confirm mode, cancel the payment
        if (State is EftposBuiState eftState && eftState.ConfirmMode)
        {
            SendMessage(new EftposCancelMessage());
        }
        Close();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        _window.UpdateState(state);
    }

    protected override void Dispose(bool disposing)
    {
        _window.Close();
        base.Dispose(disposing);
    }
}
