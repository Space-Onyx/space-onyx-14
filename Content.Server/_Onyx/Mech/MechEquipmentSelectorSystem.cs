using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared._Onyx.Mech;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Mech.Systems;

public sealed partial class MechSystem
{
    private void InitializeEquipmentSelector()
    {
        SubscribeLocalEvent<MechComponent, MechToggleEquipmentEvent>(OnOpenEquipmentSelector);
        SubscribeLocalEvent<MechComponent, MechEquipmentSelectMessage>(OnSelectEquipment);
    }

    private void OnOpenEquipmentSelector(EntityUid uid, MechComponent component, MechToggleEquipmentEvent args)
    {
        if (args.Handled || component.PilotSlot.ContainedEntity is not { } pilot ||
            !TryComp<ActorComponent>(pilot, out var actor))
            return;

        args.Handled = true;
        _ui.TryToggleUi(uid, MechUiKey.EquipmentSelector, actor.PlayerSession);
    }

    private void OnSelectEquipment(EntityUid uid, MechComponent component, MechEquipmentSelectMessage args)
    {
        if (args.Actor != component.PilotSlot.ContainedEntity)
            return;

        EntityUid? equipment = args.Equipment is { } netEntity ? GetEntity(netEntity) : null;
        if (equipment is { } selected && !component.EquipmentContainer.Contains(selected))
            return;

        if (component.CurrentSelectedEquipment is { } oldEquipment &&
            TryComp<GunComponent>(oldEquipment, out var oldGun))
            _gun.CancelShooting((oldEquipment, oldGun));

        component.CurrentSelectedEquipment = equipment;
        Dirty(uid, component);

        var popup = equipment is { }
            ? Loc.GetString("mech-equipment-select-popup", ("item", equipment))
            : Loc.GetString("mech-equipment-select-none-popup");
        _popup.PopupEntity(popup, uid, args.Actor);
        _ui.CloseUi(uid, MechUiKey.EquipmentSelector, args.Actor);
    }
}
