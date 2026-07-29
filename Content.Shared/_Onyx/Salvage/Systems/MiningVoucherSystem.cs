using Content.Shared._Onyx.Salvage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Systems;

public sealed partial class MiningVoucherSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MiningVoucherComponent, AfterInteractEvent>(OnAfterInteract);
        Subs.BuiEvents<MiningVendorComponent>(MiningVoucherUiKey.Key, subs =>
            subs.Event<MiningVoucherSelectMessage>(OnSelect));
    }

    private void OnAfterInteract(Entity<MiningVoucherComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach || _whitelist.IsWhitelistFail(ent.Comp.VendorWhitelist, target))
            return;

        args.Handled = true;
        if (!_power.IsPowered(target))
        {
            _popup.PopupEntity(Loc.GetString("mining-voucher-unpowered", ("vendor", target)), target, args.User);
            return;
        }

        _ui.TryOpenUi(target, MiningVoucherUiKey.Key, args.User);
    }

    private void OnSelect(Entity<MiningVendorComponent> ent, ref MiningVoucherSelectMessage args)
    {
        if (args.Index < 0 || args.Index >= ent.Comp.Kits.Count || !_power.IsPowered(ent.Owner))
            return;

        foreach (var item in _hands.EnumerateHeld(args.Actor))
        {
            if (!TryComp<MiningVoucherComponent>(item, out var voucher) ||
                _whitelist.IsWhitelistFail(voucher.VendorWhitelist, ent))
                continue;

            var kit = _proto.Index(ent.Comp.Kits[args.Index]);
            _popup.PopupEntity(Loc.GetString("mining-voucher-selected", ("kit", Loc.GetString(kit.Name))), args.Actor, args.Actor);
            Redeem(ent, (item, voucher), args.Index, args.Actor);
            return;
        }
    }

    private void Redeem(Entity<MiningVendorComponent> ent, Entity<MiningVoucherComponent> voucher, int index, EntityUid user)
    {
        if (_net.IsClient)
            return;

        var xform = Transform(ent);
        foreach (var id in _proto.Index(ent.Comp.Kits[index]).Content)
            SpawnNextToOrDrop(id, ent, xform);

        _audio.PlayPredicted(voucher.Comp.RedeemSound, ent, user);
        QueueDel(voucher);
    }
}
