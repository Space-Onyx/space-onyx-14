using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared._Onyx.Surgery.Augments;

namespace Content.Server._Onyx.Surgery.Augments;

public sealed partial class AugmentToolPanelSystem : EntitySystem
{
    [Dependency] private AugmentSystem _augment = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AugmentToolPanelComponent, AugmentLostPowerEvent>(OnLostPower);
        Subs.BuiEvents<AugmentToolPanelComponent>(AugmentToolPanelUiKey.Key,
            subs => subs.Event<AugmentToolPanelSwitchMessage>(OnSwitchTool));
    }

    private void OnLostPower(Entity<AugmentToolPanelComponent> ent, ref AugmentLostPowerEvent args) =>
        SwitchTool(ent, null, args.Body, false);

    private void OnSwitchTool(Entity<AugmentToolPanelComponent> ent, ref AugmentToolPanelSwitchMessage args)
    {
        if (_augment.GetBody(ent.Owner) is not { } body || !_augment.CanUse(ent.Owner, body))
            return;
        if (ent.Comp.RequiresPower && !_augment.TryUseCharge(body, ent.Comp.SwitchCharge, body))
            return;
        SwitchTool(ent, GetEntity(args.DesiredTool), body, true);
    }

    private void SwitchTool(Entity<AugmentToolPanelComponent> augment, EntityUid? desired, EntityUid body, bool popup)
    {
        if (!TryComp(body, out HandsComponent? hands) ||
            !TryComp(Transform(augment).ParentUid, out BodyPartComponent? part))
            return;

        var location = part.Symmetry switch
        {
            BodyPartSymmetry.Left => HandLocation.Left,
            BodyPartSymmetry.Right => HandLocation.Right,
            _ => HandLocation.Middle,
        };
        var hand = hands.Hands.FirstOrDefault(pair => pair.Value.Location == location).Key;
        if (hand == null)
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("augment-tool-panel-no-hand"), body, body, PopupType.LargeCaution);
            return;
        }

        if (_hands.GetHeldItem(body, hand) is { } held)
        {
            if (!RemComp<AugmentToolPanelActiveItemComponent>(held))
            {
                if (popup)
                    _popup.PopupEntity(Loc.GetString("augment-tool-panel-hand-full"), body, body, PopupType.SmallCaution);
                return;
            }
            if (!_storage.PlayerInsertEntityInWorld(augment.Owner, body, held))
            {
                EnsureComp<AugmentToolPanelActiveItemComponent>(held);
                return;
            }
            _toggle.TryDeactivate(augment.Owner, body);
            if (desired == null && popup)
                _popup.PopupEntity(Loc.GetString("augment-tool-panel-retracted", ("item", held)), body, body);
        }

        if (desired is not { } tool)
            return;
        if (!TryComp(augment.Owner, out Content.Shared.Storage.StorageComponent? storage) ||
            !storage.StoredItems.ContainsKey(tool) || !_hands.TryPickup(body, tool, hand))
            return;

        EnsureComp<AugmentToolPanelActiveItemComponent>(tool);
        _toggle.TryActivate(augment.Owner, body);
        if (popup)
            _popup.PopupEntity(Loc.GetString("augment-tool-panel-selected", ("item", tool)), body, body);
    }
}
