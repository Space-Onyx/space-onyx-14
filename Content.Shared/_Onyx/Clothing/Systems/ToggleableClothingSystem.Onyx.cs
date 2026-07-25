using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;

namespace Content.Shared.Clothing.EntitySystems;

public sealed partial class ToggleableClothingSystem
{
    public bool IsAttachedStored(EntityUid toggleable, EntityUid attached,
        ToggleableClothingComponent? component = null)
    {
        return Resolve(toggleable, ref component, false) && component.Container?.Contains(attached) == true;
    }

    private void OnToggleableUnequipAttempt(Entity<ToggleableClothingComponent> toggleable,
        ref BeingUnequippedAttemptEvent args)
    {
        var component = toggleable.Comp;
        if (!component.BlockUnequipWhenAttached || component.Container == null)
            return;

        foreach (var (part, slot) in component.ClothingUids)
        {
            if (component.Container.Contains(part))
                continue;

            if (!_inventorySystem.TryGetSlotEntity(args.UnEquipTarget, slot, out var equipped) || equipped != part)
                continue;

            args.Reason = "toggleable-clothing-remove-all-attached-first";
            args.Cancel();
            return;
        }
    }

    private void StartAttachedDoAfter(EntityUid user,
        EntityUid attachedUid,
        AttachedClothingComponent attached,
        EntityUid wearer,
        ToggleableClothingComponent toggleable,
        string slot)
    {
        if (toggleable.StripDelay == null)
            return;

        var (time, stealth) = _strippable.GetStripTimeModifiers(user, wearer, attached.AttachedUid,
            toggleable.StripDelay.Value * 3 / 4);
        var doAfter = new DoAfterArgs(EntityManager, user, time, new AttachClothingDoAfterEvent(), attachedUid,
            wearer, attachedUid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = 2,
        };

        if (!_doAfter.TryStartDoAfter(doAfter) || stealth)
            return;

        var popup = Loc.GetString("strippable-component-alert-owner-interact",
            ("user", IdentityManagement.Identity.Entity(user, EntityManager)), ("item", attachedUid));
        _popupSystem.PopupEntity(popup, wearer, wearer, PopupType.Large);
    }

    private void OnAttachedDoAfterComplete(Entity<AttachedClothingComponent> attached,
        ref AttachClothingDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null ||
            !TryComp(attached.Comp.AttachedUid, out ToggleableClothingComponent? toggleable) ||
            !toggleable.ClothingUids.TryGetValue(attached, out var slot))
            return;

        _inventorySystem.TryUnequip(args.User, args.Target.Value, slot, force: true);
    }

    private EntityUid? FindSuitStorage(EntityUid wearer)
    {
        if (!_inventorySystem.TryGetSlotEntity(wearer, "suitstorage", out var item) ||
            !_inventorySystem.TryUnequip(wearer, "suitstorage", silent: true))
            return null;

        return item;
    }

    private void RestoreSuitStorage(EntityUid wearer, EntityUid? item)
    {
        if (item != null && !_inventorySystem.TryEquip(wearer, item.Value, "suitstorage", silent: true))
            _popupSystem.PopupEntity(Loc.GetString("inventory-component-dropped-from-unequip", ("items", 1)),
                wearer, wearer);
    }
}
