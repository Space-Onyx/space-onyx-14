using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared._Onyx.Surgery.Augments.NeuroInterface;

namespace Content.Shared._Onyx.Surgery.Augments;

public sealed partial class AugmentSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedNeuroInterfaceSystem _neuroInterface = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeLifecycle();
        InitializeActions();
        InitializePower();
        InitializeStrength();
        SubscribeLocalEvent<MantisBladeComponent, GetMeleeAttackRateEvent>(OnMantisBladeAttackRate);
    }

    private void OnMantisBladeAttackRate(Entity<MantisBladeComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        if (!TryComp(ent, out AugmentPowerReceiverComponent? receiver) || receiver.Provider is not { } provider ||
            GetBody(provider) is not { } body || body != args.User ||
            !TryComp(body, out InstalledAugmentsComponent? installed))
            return;

        var deployedBlades = ResolveAugments(installed).Count(augment =>
            TryComp(augment, out AugmentItemPanelComponent? panel) && panel.IsEquipped &&
            panel.SpawnedItem is { } item && HasComp<MantisBladeComponent>(item));
        if (deployedBlades >= 2)
            args.Multipliers *= 1.5f;
    }

    public EntityUid? GetBody(EntityUid augment) => CompOrNull<OrganComponent>(augment)?.Body;

    public IEnumerable<EntityUid> ResolveAugments(InstalledAugmentsComponent installed)
    {
        foreach (var net in installed.Augments)
        {
            var uid = GetEntity(net);
            if (Exists(uid))
                yield return uid;
        }
    }

    public bool HasInstalled<T>(EntityUid body) where T : Component
    {
        return TryComp(body, out InstalledAugmentsComponent? installed) && ResolveAugments(installed).Any(HasComp<T>);
    }

    public IEnumerable<EntityUid> GetPowerSlots(EntityUid body)
    {
        if (!TryComp(body, out InstalledAugmentsComponent? installed))
            yield break;
        foreach (var augment in ResolveAugments(installed))
        {
            if (HasComp<AugmentPowerCellSlotComponent>(augment))
                yield return augment;
        }
    }

    public bool TryUseCharge(EntityUid body, float charge, EntityUid? user = null)
    {
        if (charge <= 0f || !HasPower(body, charge))
            return charge <= 0f;

        foreach (var battery in GetBatteries(body))
        {
            var available = _battery.GetCharge(battery.AsNullable());
            var used = Math.Min(charge, available);
            if (used > 0f)
                _battery.UseCharge(battery.AsNullable(), used);
            charge -= used;
            if (charge <= 0f)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Draws energy for an augment-owned consumer. Consumers only need an
    /// <see cref="AugmentPowerReceiverComponent"/> linked to their provider augment.
    /// </summary>
    public bool TryUsePower(Entity<AugmentPowerReceiverComponent?> receiver, float charge, EntityUid? user = null)
    {
        if (charge <= 0f || !Resolve(receiver, ref receiver.Comp, false) || receiver.Comp.Provider is not { } provider ||
            GetBody(provider) is not { } body || !IsEnabled(provider))
            return false;

        return TryUseCharge(body, charge, user);
    }

    public void SetPowerProvider(EntityUid receiver, EntityUid? provider)
    {
        var component = EnsureComp<AugmentPowerReceiverComponent>(receiver);
        component.Provider = provider;
        Dirty(receiver, component);
    }

    public bool HasPower(EntityUid body, float charge = 0.01f)
    {
        var available = 0f;
        foreach (var battery in GetBatteries(body))
            available += _battery.GetCharge(battery.AsNullable());
        return available >= charge;
    }

    public bool TryGetBattery(EntityUid slot, out Entity<BatteryComponent> battery)
    {
        battery = default;
        if (!_powerCell.TryGetBatteryFromSlot(slot, out var found))
            return false;
        battery = found.Value;
        return true;
    }

    public IEnumerable<Entity<BatteryComponent>> GetBatteries(EntityUid body)
    {
        if (!TryComp(body, out InstalledAugmentsComponent? installed))
            yield break;

        foreach (var augment in ResolveAugments(installed))
        {
            if (!TryComp(augment, out AugmentBatteryBankComponent? bank))
                continue;
            foreach (var slot in bank.Slots)
            {
                if (_itemSlots.GetItemOrNull(augment, slot) is { } item && TryComp(item, out BatteryComponent? battery))
                    yield return (item, battery);
            }
        }
    }

    public bool CanUse(EntityUid augment, EntityUid user) =>
        GetBody(augment) == user && IsEnabled(augment) && _neuroInterface.GetEfficiency(user, augment) > 0f &&
        (!HasComp<AugmentPowerDrawComponent>(augment) || HasPower(user));

    public float GetEfficiency(EntityUid body, EntityUid augment) =>
        _neuroInterface.GetEfficiency(body, augment);

    private bool IsEnabled(EntityUid augment) =>
        !TryComp(augment, out CyberneticsComponent? cyber) || !cyber.Disabled;

    private void Disable(EntityUid augment)
    {
        _toggle.TryDeactivate(augment);
        if (TryComp(augment, out AugmentActionComponent? action))
            _actions.SetToggled(action.ActionEntity, false);
        if (TryComp(augment, out AugmentActivatableUIComponent? ui) && ui.Key is { } key)
            _ui.CloseUi(augment, key);
    }
}
