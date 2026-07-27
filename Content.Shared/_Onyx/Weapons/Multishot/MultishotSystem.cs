using System.Linq;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Weapons.Multishot;

public sealed partial class MultishotSystem : EntitySystem
{
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MultishotComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<MultishotComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<MultishotComponent, GunRefreshModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<MultishotComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<MultishotComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<MultishotComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MissChanceComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<HandsComponent, MultishotShootRequestEvent>(OnShootRequest);
    }

    private void OnShootRequest(Entity<HandsComponent> ent, ref MultishotShootRequestEvent args)
    {
        if (!_combat.IsInCombatMode(ent.Owner))
            return;

        var guns = GetGuns(ent.Owner);
        var requestedGun = GetEntity(args.Request.Gun);
        if (guns.Count < 2 || guns.All(gun => gun.Owner != requestedGun))
            return;

        var coordinates = GetCoordinates(args.Request.Coordinates);
        var target = GetEntity(args.Request.Target);
        foreach (var gun in guns)
            _gun.AttemptShoot(ent.Owner, (gun.Owner, gun.Comp1), coordinates, target);
        args.Handled = true;
    }

    private void OnEquipped(Entity<MultishotComponent> ent, ref GotEquippedHandEvent args) => RefreshHeld(args.User);

    private void OnUnequipped(Entity<MultishotComponent> ent, ref GotUnequippedHandEvent args)
    {
        ent.Comp.MultishotAffected = false;
        Dirty(ent);
        _gun.RefreshModifiers(ent.Owner);
        RefreshHeld(args.User);
    }

    private void RefreshHeld(EntityUid user)
    {
        var guns = GetGuns(user);
        var affected = guns.Count >= 2;
        foreach (var gun in guns)
        {
            gun.Comp2.MultishotAffected = affected;
            Dirty(gun.Owner, gun.Comp2);
            _gun.RefreshModifiers(gun.Owner);
        }
    }

    private void OnRefresh(Entity<MultishotComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!ent.Comp.MultishotAffected)
            return;
        args.MaxAngle = args.MaxAngle * ent.Comp.SpreadMultiplier + Angle.FromDegrees(ent.Comp.SpreadAddition);
        args.MinAngle = args.MinAngle * ent.Comp.SpreadMultiplier + Angle.FromDegrees(ent.Comp.SpreadAddition);
    }

    private void OnShot(Entity<MultishotComponent> ent, ref GunShotEvent args)
    {
        if (!ent.Comp.MultishotAffected)
            return;

        if (ent.Comp.StaminaDamage != 0)
            _stamina.TakeStaminaDamage(args.User, ent.Comp.StaminaDamage, source: args.User, with: ent.Owner, visual: false);

        if (ent.Comp.HandDamageAmount == 0 || !_hands.IsHolding(args.User, ent.Owner))
            return;

        var damage = new DamageSpecifier(_prototypes.Index<DamageTypePrototype>(ent.Comp.HandDamageType), ent.Comp.HandDamageAmount);
        _damage.TryChangeDamage(args.User, damage, origin: args.User);
    }

    private void OnAmmoShot(Entity<MultishotComponent> ent, ref AmmoShotEvent args)
    {
        if (!ent.Comp.MultishotAffected || _net.IsClient)
            return;
        foreach (var projectile in args.FiredProjectiles)
            EnsureComp<MissChanceComponent>(projectile).Chance = ent.Comp.MissChance;
    }

    private void OnPreventCollide(Entity<MissChanceComponent> ent, ref PreventCollideEvent args)
    {
        var random = new RobustRandom();
        random.SetSeed((int) _timing.CurTick.Value + GetNetEntity(ent).Id);
        if (!args.Cancelled && HasComp<MobStateComponent>(args.OtherEntity) && random.NextFloat() < ent.Comp.Chance)
            args.Cancelled = true;
    }

    private void OnExamined(Entity<MultishotComponent> ent, ref ExaminedEvent args)
    {
        var message = new FormattedMessage();
        message.AddText(Loc.GetString(ent.Comp.ExamineMessage, ("chance", MathF.Round(ent.Comp.MissChance * 100f))));
        args.PushMessage(message);
    }

    private List<Entity<GunComponent, MultishotComponent>> GetGuns(EntityUid user)
    {
        var guns = new List<Entity<GunComponent, MultishotComponent>>();
        foreach (var held in _hands.EnumerateHeld(user))
            if (TryComp<GunComponent>(held, out var gun) && TryComp<MultishotComponent>(held, out var multishot))
                guns.Add((held, gun, multishot));
        return guns;
    }
}

[ByRefEvent]
public record struct MultishotShootRequestEvent(RequestShootEvent Request, bool Handled = false);
