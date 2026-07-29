using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Salvage.Weapons;

public abstract partial class SharedBlockChargeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlockChargeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BlockChargeComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<BlockChargeUserComponent, BeforeDamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<BlockChargeComponent, ApplyMarkerBonusEvent>(OnMarkerBonus);
        SubscribeLocalEvent<BlockChargeComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<BlockChargeComponent, GotUnequippedHandEvent>(OnUnequipped);
    }

    private void OnMapInit(Entity<BlockChargeComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.RechargeTime);
        Dirty(ent);
    }

    private void OnExamine(Entity<BlockChargeComponent> ent, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString(ent.Comp.HasCharge
            ? "block-charge-status-charged"
            : "block-charge-status-recharging"));

    private void OnMarkerBonus(Entity<BlockChargeComponent> ent, ref ApplyMarkerBonusEvent args)
    {
        var reduced = ent.Comp.NextCharge - TimeSpan.FromSeconds(ent.Comp.MarkerReductionTime);
        ent.Comp.NextCharge = reduced < _timing.CurTime ? _timing.CurTime : reduced;
        Dirty(ent);
    }

    private void OnDamage(Entity<BlockChargeUserComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || args.Origin == null || !HasComp<FaunaComponent>(args.Origin.Value))
            return;

        EntityUid? weapon = null;
        BlockChargeComponent? block = null;
        foreach (var candidate in ent.Comp.BlockingWeapons)
        {
            if (TryComp<BlockChargeComponent>(candidate, out var candidateBlock) && candidateBlock.HasCharge)
            {
                weapon = candidate;
                block = candidateBlock;
                break;
            }
        }

        if (weapon == null || block == null)
            return;

        _popup.PopupEntity(Loc.GetString("block-attack-notice",
            ("user", ent.Owner),
            ("blocked", args.Origin.Value)), ent.Owner, ent.Owner);
        block.HasCharge = false;
        block.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(block.RechargeTime);
        Dirty(weapon.Value, block);
        args.Cancelled = true;
    }

    private void OnEquipped(Entity<BlockChargeComponent> ent, ref GotEquippedHandEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var user = EnsureComp<BlockChargeUserComponent>(args.User);
        if (!user.BlockingWeapons.Contains(ent))
            user.BlockingWeapons.Add(ent);
        if (ent.Comp.HasCharge)
            _popup.PopupEntity(Loc.GetString("block-charge-startup", ("entity", ent.Owner)), args.User, args.User);
        Dirty(args.User, user);
    }

    private void OnUnequipped(Entity<BlockChargeComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!_timing.IsFirstTimePredicted || !TryComp<BlockChargeUserComponent>(args.User, out var user))
            return;

        user.BlockingWeapons.Remove(ent);
        if (user.BlockingWeapons.Count == 0)
            RemCompDeferred<BlockChargeUserComponent>(args.User);
        else
            Dirty(args.User, user);
    }
}
