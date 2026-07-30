using Content.Shared._Onyx.GPS;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.GPS;

[UsedImplicitly]
public sealed class GpsBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private GpsWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<GpsWindow>();
        _window.SetOwner(Owner);
        _window.OnClose += Close;
        _window.TrackedEntitySelected += entity =>
        {
            _window.SetPendingTrackedEntity(entity);
            SendPredictedMessage(new GpsSetTrackedEntityMessage(entity));
        };
        _window.GpsNameChanged += name => SendPredictedMessage(new GpsSetGpsNameMessage(name));
        _window.DistressPressed += distress =>
        {
            _window.SetPendingDistress(distress);
            SendPredictedMessage(new GpsSetInDistressMessage(distress));
        };
        _window.EnabledPressed += enabled =>
        {
            _window.SetPendingEnabled(enabled);
            SendPredictedMessage(new GpsSetEnabledMessage(enabled));
        };
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is GpsUpdateMessage update)
            _window?.UpdateState(Owner, update);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Close();
        _window = null;
    }
}
