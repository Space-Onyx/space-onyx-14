using Content.Server.Fax;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Fax.Components;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Fax;

public sealed partial class FaxAlertSystem : EntitySystem
{
    [Dependency] private RadioSystem _radioSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FaxMachineComponent, FaxReceivedEvent>(OnFaxReceived);
    }

    private void OnFaxReceived(Entity<FaxMachineComponent> faxEntity, ref FaxReceivedEvent args)
    {
        if (!TryComp(faxEntity, out FaxAlertComponent? alertComponent))
            return;

        var senderName = args.FromAddress != null &&
            faxEntity.Comp.KnownFaxes.TryGetValue(args.FromAddress, out var faxName)
                ? faxName
                : Loc.GetString("fax-machine-popup-source-unknown");

        var alertMessage = Loc.GetString("fax-alert-message",
            ("sender", senderName),
            ("receiver", faxEntity.Comp.FaxName));

        _radioSystem.SendRadioMessage(
            faxEntity,
            alertMessage,
            _prototypeManager.Index<RadioChannelPrototype>(alertComponent.AlertChannel),
            faxEntity);
    }
}
