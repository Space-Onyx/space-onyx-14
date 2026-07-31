using System.Numerics;
using Content.Server._Onyx.SpaceArtillery.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Onyx.SpaceArtillery;
using Content.Shared.Camera;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Onyx.SpaceArtillery;

public sealed partial class SpaceArtillerySystem : EntitySystem
{
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;

    private const float TargetDistance = 100f;
    private const float BigDamage = 1000f;
    private const float BigDamageKick = 35f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpaceArtilleryComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SpaceArtilleryComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<SpaceArtilleryComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SpaceArtilleryComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<ShipWeaponProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnStartup(Entity<SpaceArtilleryComponent> artillery, ref ComponentStartup args)
    {
        _deviceLink.EnsureSinkPorts(artillery, artillery.Comp.SpaceArtilleryFirePort);

        if (TryComp<ApcPowerReceiverComponent>(artillery, out var receiver))
            UpdatePowerLoad(artillery, receiver);

        UpdateRechargeRate(artillery);
    }

    private void OnSignalReceived(Entity<SpaceArtilleryComponent> artillery, ref SignalReceivedEvent args)
    {
        if (args.Port != artillery.Comp.SpaceArtilleryFirePort ||
            !TryComp<BatteryComponent>(artillery, out var battery) ||
            _battery.GetCharge((artillery.Owner, battery)) < artillery.Comp.PowerUseActive ||
            !_gun.TryGetGun(artillery, out var gun) ||
            !_gun.CanShoot(gun.Comp))
        {
            return;
        }

        var xform = Transform(artillery);
        if (xform.MapUid is not { } mapUid)
            return;

        var position = _transform.GetWorldPosition(artillery);
        var rotation = _transform.GetWorldRotation(artillery) + Math.PI;
        var target = new Vector2(
            position.X - TargetDistance * (float) Math.Sin(rotation),
            position.Y + TargetDistance * (float) Math.Cos(rotation));

        if (_gun.AttemptShoot(artillery, gun, new EntityCoordinates(mapUid, target)))
            _battery.UseCharge((artillery.Owner, battery), artillery.Comp.PowerUseActive);
    }

    private void OnPowerChanged(Entity<SpaceArtilleryComponent> artillery, ref PowerChangedEvent args)
    {
        UpdateRechargeRate(artillery, args.Powered);
    }

    private void OnChargeChanged(Entity<SpaceArtilleryComponent> artillery, ref ChargeChangedEvent args)
    {
        if (TryComp<ApcPowerReceiverComponent>(artillery, out var receiver))
            UpdatePowerLoad(artillery, receiver);
    }

    private void UpdateRechargeRate(Entity<SpaceArtilleryComponent> artillery, bool? powered = null)
    {
        if (!TryComp<BatterySelfRechargerComponent>(artillery, out var recharger) ||
            !TryComp<BatteryComponent>(artillery, out var battery))
        {
            return;
        }

        powered ??= !TryComp<ApcPowerReceiverComponent>(artillery, out var receiver) || receiver.Powered;
        recharger.AutoRechargeRate = powered.Value
            ? artillery.Comp.PowerChargeRate
            : -artillery.Comp.PowerUsePassive;
        Dirty(artillery.Owner, recharger);
        _battery.RefreshChargeRate((artillery.Owner, battery));
    }

    private void UpdatePowerLoad(Entity<SpaceArtilleryComponent> artillery, ApcPowerReceiverComponent receiver)
    {
        var batteryFull = TryComp<BatteryComponent>(artillery, out var battery) &&
            _battery.GetCharge((artillery.Owner, battery)) >= battery.MaxCharge * 0.99f;
        receiver.Load = artillery.Comp.PowerUsePassive + (batteryFull ? 0f : artillery.Comp.PowerChargeRate);
    }

    private void OnProjectileHit(Entity<ShipWeaponProjectileComponent> projectile, ref ProjectileHitEvent args)
    {
        if (Transform(args.Target).GridUid is not { } grid)
            return;

        var players = Filter.Empty().AddInGrid(grid);
        foreach (var player in players.Recipients)
        {
            if (player.AttachedEntity is not { } playerEntity)
                continue;

            var direction = _transform.GetWorldPosition(projectile) - _transform.GetWorldPosition(playerEntity);
            if (direction == Vector2.Zero)
                continue;

            _recoil.KickCamera(
                playerEntity,
                direction.Normalized() * (float) args.Damage.GetTotal() / BigDamage * BigDamageKick);
        }
    }
}
