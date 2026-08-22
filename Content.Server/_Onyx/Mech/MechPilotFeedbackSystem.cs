using Content.Server.Chat.Systems;
using Content.Shared._Onyx.Mech;
using Content.Shared.Chat;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Systems;

namespace Content.Server._Onyx.Mech;

public sealed partial class MechPilotFeedbackSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformComponent, OnVehicleEnteredEvent>(OnEntered);
        SubscribeLocalEvent<TransformComponent, OnVehicleExitedEvent>(OnExited);
    }

    private void OnEntered(Entity<TransformComponent> pilot, ref OnVehicleEnteredEvent args)
    {
        if (!TryComp<MechComponent>(args.Vehicle, out var mech))
            return;

        UpdatePilotVision(args.Vehicle, mech);
        Speak(args.Vehicle, "mech-pilot-connected");
    }

    private void OnExited(Entity<TransformComponent> pilot, ref OnVehicleExitedEvent args)
    {
        if (!HasComp<MechComponent>(args.Vehicle))
            return;

        RemComp<MechPowerBlindnessComponent>(pilot);
        _blindable.UpdateIsBlind(pilot.Owner);
        Speak(args.Vehicle, "mech-pilot-disconnected", emergency: true);
    }

    public void UpdatePilotVision(EntityUid mechUid, MechComponent? mech = null)
    {
        if (!Resolve(mechUid, ref mech, false) ||
            !_vehicle.TryGetOperator(mechUid, out var operatorEntity))
            return;

        var pilot = operatorEntity.Value.Owner;

        if (mech.Energy <= 0)
            EnsureComp<MechPowerBlindnessComponent>(pilot);
        else
            RemComp<MechPowerBlindnessComponent>(pilot);

        _blindable.UpdateIsBlind(pilot);
    }

    private void Speak(EntityUid mech, string message, bool emergency = false)
    {
        if (!TryComp<MechComponent>(mech, out var component))
            return;

        if (component.Energy <= 0)
        {
            if (!emergency)
                return;

            message = "mech-emergency-eject";
        }

        _chat.TrySendInGameICMessage(
            mech,
            Loc.GetString(message),
            InGameICChatType.Speak,
            hideChat: false,
            checkRadioPrefix: false,
            ignoreActionBlocker: true);
    }
}
