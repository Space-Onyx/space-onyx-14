using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Upgrades;
using Content.Shared.Weapons.Ranged.Upgrades.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Weapons.Ranged;

public sealed partial class PKAUpgradeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private GunUpgradeSystem _upgrades = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedHandheldLightSystem _handheldLight = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GunUpgradeVampirismComponent, AmmoShotEvent>(OnVampirismShot);
        SubscribeLocalEvent<ProjectileVampirismComponent, ProjectileHitEvent>(OnVampirismHit);
        SubscribeLocalEvent<GunUpgradeFireRateComponent, RechargeBasicEntityAmmoGetCooldownModifiersEvent>(OnRecharge);
        SubscribeLocalEvent<PKAUpgradeEjectableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PKAUpgradeEjectableComponent, PKAUpgradeEjectDoAfterEvent>(OnEject);
        SubscribeLocalEvent<PKAWeaponAttachmentsComponent, GetRelayMeleeWeaponEvent>(OnGetMeleeRelay);
        SubscribeLocalEvent<PKAWeaponAttachmentsComponent, GetVerbsEvent<ActivationVerb>>(OnGetFlashlightVerb);
        SubscribeLocalEvent<GunUpgradeFlashlightComponent, EntGotInsertedIntoContainerMessage>(OnFlashlightInserted);
        SubscribeLocalEvent<GunUpgradeFlashlightComponent, EntGotRemovedFromContainerMessage>(OnFlashlightRemoved);
        SubscribeLocalEvent<GunUpgradeFlashlightComponent, LightToggleEvent>(OnFlashlightToggled);
    }

    private void OnRecharge(Entity<GunUpgradeFireRateComponent> ent, ref RechargeBasicEntityAmmoGetCooldownModifiersEvent args)
    {
        args.Cooldown /= ent.Comp.Coefficient;
    }

    private void OnVampirismShot(Entity<GunUpgradeVampirismComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var ammo in args.FiredProjectiles)
        {
            if (!HasComp<ProjectileComponent>(ammo))
                continue;

            EnsureComp<ProjectileVampirismComponent>(ammo).DamageOnHit = ent.Comp.DamageOnHit;
        }
    }

    private void OnVampirismHit(Entity<ProjectileVampirismComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Shooter is { } shooter && HasComp<MobStateComponent>(args.Target))
            _damage.TryChangeDamage(shooter, ent.Comp.DamageOnHit, interruptsDoAfters: false, origin: shooter);
    }

    private void OnGetVerbs(Entity<PKAUpgradeEjectableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<UpgradeableGunComponent>(ent, out var upgradeable))
            return;

        foreach (var upgrade in _upgrades.GetCurrentUpgrades((ent, upgradeable)))
        {
            var user = args.User;
            var upgradeUid = upgrade.Owner;
            args.Verbs.Add(new AlternativeVerb
            {
                Category = VerbCategory.Eject,
                Text = Name(upgradeUid),
                Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.EjectDelay,
                    new PKAUpgradeEjectDoAfterEvent(GetNetEntity(upgradeUid)), ent, target: ent)
                {
                    BreakOnMove = true,
                    NeedHand = false,
                }),
            });
        }
    }

    private void OnEject(Entity<PKAUpgradeEjectableComponent> ent, ref PKAUpgradeEjectDoAfterEvent args)
    {
        var upgrade = GetEntity(args.Upgrade);
        if (args.Cancelled || args.Handled || !Exists(upgrade) ||
            !_containers.TryGetContainingContainer(upgrade, out var container) || container.Owner != ent.Owner ||
            !_containers.Remove(upgrade, container, destination: Transform(args.User).Coordinates))
            return;

        args.Handled = true;
        _hands.TryPickupAnyHand(args.User, upgrade);
        _audio.PlayPredicted(ent.Comp.EjectSound, ent, args.User);
        _gun.RefreshModifiers(ent.Owner);
    }

    private void OnGetMeleeRelay(Entity<PKAWeaponAttachmentsComponent> ent, ref GetRelayMeleeWeaponEvent args)
    {
        if (args.Handled || GetAttachment<GunUpgradeBayonetComponent>(ent) is not { } bayonet)
            return;

        args.Found = bayonet;
        args.Handled = true;
    }

    private void OnGetFlashlightVerb(Entity<PKAWeaponAttachmentsComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract ||
            GetAttachment<GunUpgradeFlashlightComponent>(ent) is not { } flashlight ||
            !TryComp<HandheldLightComponent>(flashlight, out var light))
            return;

        var user = args.User;
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("verb-common-toggle-light"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/light.svg.192dpi.png")),
            Act = light.Activated
                ? () => _handheldLight.TurnOff((flashlight, light))
                : () => _handheldLight.TurnOn(user, (flashlight, light)),
        });
    }

    private EntityUid? GetAttachment<T>(EntityUid owner) where T : IComponent
    {
        foreach (var container in _containers.GetAllContainers(owner))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (HasComp<T>(contained))
                    return contained;
            }
        }

        return null;
    }

    private void OnFlashlightInserted(Entity<GunUpgradeFlashlightComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (HasComp<PKAWeaponAttachmentsComponent>(args.Container.Owner) &&
            TryComp<HandheldLightComponent>(ent, out var light))
            _appearance.SetData(args.Container.Owner, PKAAttachmentVisuals.FlashlightEnabled, light.Activated);
    }

    private void OnFlashlightToggled(Entity<GunUpgradeFlashlightComponent> ent, ref LightToggleEvent args)
    {
        if (_containers.TryGetContainingContainer(ent.Owner, out var container) &&
            HasComp<PKAWeaponAttachmentsComponent>(container.Owner))
            _appearance.SetData(container.Owner, PKAAttachmentVisuals.FlashlightEnabled, args.IsOn);
    }

    private void OnFlashlightRemoved(Entity<GunUpgradeFlashlightComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (HasComp<PKAWeaponAttachmentsComponent>(args.Container.Owner))
            _appearance.SetData(args.Container.Owner, PKAAttachmentVisuals.FlashlightEnabled, false);
    }
}
