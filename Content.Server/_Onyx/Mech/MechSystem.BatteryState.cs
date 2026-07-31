using Content.Shared.Mech.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Server._Onyx.Mech;
using Robust.Shared.Containers;

namespace Content.Server.Mech.Systems;

public sealed partial class MechSystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private UnpoweredFlashlightSystem _flashlight = default!;
    [Dependency] private MechPilotFeedbackSystem _pilotFeedback = default!;

    private void InitializeBatteryState()
    {
        SubscribeLocalEvent<BatteryComponent, EntGotRemovedFromContainerMessage>(OnBatteryRemoved);
        SubscribeLocalEvent<BatteryComponent, ChargeChangedEvent>(OnBatteryChargeChanged);
    }

    private void OnBatteryRemoved(EntityUid uid, BatteryComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (!TryComp<MechComponent>(args.Container.Owner, out var mech) || args.Container != mech.BatterySlot)
            return;

        SyncBatteryState(args.Container.Owner, mech);
    }

    private void OnBatteryChargeChanged(Entity<BatteryComponent> battery, ref ChargeChangedEvent args)
    {
        var parent = Transform(battery).ParentUid;
        if (!TryComp<MechComponent>(parent, out var mech) || mech.BatterySlot.ContainedEntity != battery.Owner)
            return;

        SyncBatteryState(parent, mech);
        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (TryComp<BatteryAmmoProviderComponent>(equipment, out var provider))
                _gun.UpdateShots((equipment, provider));
        }
    }

    private void SyncBatteryState(EntityUid uid, MechComponent component)
    {
        if (component.BatterySlot.ContainedEntity is { } battery &&
            TryComp<BatteryComponent>(battery, out var batteryComp))
        {
            component.Energy = _battery.GetCharge((battery, batteryComp));
            component.MaxEnergy = batteryComp.MaxCharge;
        }
        else
        {
            component.Energy = 0;
            component.MaxEnergy = 0;
        }

        Dirty(uid, component);
        if (component.Energy <= 0 && TryComp<UnpoweredFlashlightComponent>(uid, out var flashlight))
            _flashlight.SetLight((uid, flashlight), false, quiet: true);
        _pilotFeedback.UpdatePilotVision(uid, component);
        _actionBlocker.UpdateCanMove(uid);
        UpdateUserInterface(uid, component);
    }

    private bool CanEnterPoweredMech(EntityUid uid, EntityUid user, MechComponent component)
    {
        if (component.Energy > 0)
            return true;

        _popup.PopupEntity(Loc.GetString("mech-unpowered-entry-denied"), uid, user);
        return false;
    }
}
