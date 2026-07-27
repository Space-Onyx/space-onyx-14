using Content.Shared.Tag;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Upgrades;
using Content.Shared.Weapons.Ranged.Upgrades.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Mech;

public sealed partial class MechKineticUpgradeSystem : EntitySystem
{
    [Dependency] private GunUpgradeSystem _upgrades = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    private static readonly ProtoId<TagPrototype> PkaUpgrade = "PKAUpgrade";
    private static readonly ProtoId<TagPrototype> GunUpgradeRange = "GunUpgradeRange";
    private static readonly ProtoId<TagPrototype> GunUpgradeReloadSpeed = "GunUpgradeReloadSpeed";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechKineticUpgradeComponent, GunRefreshModifiersEvent>(OnRefresh,
            after: [typeof(GunUpgradeSystem)]);
        SubscribeLocalEvent<MechKineticUpgradeComponent, AfterInteractUsingEvent>(OnInteractUsing,
            before: [typeof(GunUpgradeSystem)]);
        SubscribeLocalEvent<MechKineticUpgradeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<MechKineticUpgradeComponent, MechKineticInsertDoAfterEvent>(OnInsert);
        SubscribeLocalEvent<MechKineticUpgradeComponent, MechKineticEjectDoAfterEvent>(OnEject);
    }

    private void OnRefresh(Entity<MechKineticUpgradeComponent> gun, ref GunRefreshModifiersEvent args)
    {
        if (!TryComp<UpgradeableGunComponent>(gun, out var upgradeable))
            return;

        foreach (var upgrade in _upgrades.GetCurrentUpgrades((gun, upgradeable)))
        {
            if (_tags.HasTag(upgrade, GunUpgradeRange))
                args.ProjectileSpeed *= 1.3f / 1.5f;
            if (_tags.HasTag(upgrade, GunUpgradeReloadSpeed))
                args.FireRate *= 1.2f / 1.5f;
        }
    }

    private void OnInteractUsing(Entity<MechKineticUpgradeComponent> gun, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || !_tags.HasTag(args.Used, PkaUpgrade) || GetUpgrade(gun) != null)
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, gun.Comp.InsertDelay,
            new MechKineticInsertDoAfterEvent(), gun, target: args.Used, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnGetVerbs(Entity<MechKineticUpgradeComponent> gun, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || GetUpgrade(gun) is not { })
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Category = VerbCategory.Eject,
            Text = Loc.GetString("verb-categories-eject"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, gun.Comp.EjectDelay,
                new MechKineticEjectDoAfterEvent(), gun, target: gun)
            {
                BreakOnMove = true,
                NeedHand = false,
            }),
        });
    }

    private void OnInsert(Entity<MechKineticUpgradeComponent> gun, ref MechKineticInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } upgrade ||
            !_tags.HasTag(upgrade, PkaUpgrade) || GetUpgrade(gun) != null ||
            !_containers.TryGetContainer(gun, "upgrades", out var container) ||
            !_containers.Insert(upgrade, container))
            return;

        args.Handled = true;
        _gun.RefreshModifiers(gun.Owner);
    }

    private void OnEject(Entity<MechKineticUpgradeComponent> gun, ref MechKineticEjectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || GetUpgrade(gun) is not { } upgrade ||
            !_containers.TryGetContainingContainer(upgrade, out var container) ||
            !_containers.Remove(upgrade, container, destination: Transform(args.User).Coordinates))
            return;

        args.Handled = true;
        _gun.RefreshModifiers(gun.Owner);
    }

    private EntityUid? GetUpgrade(EntityUid gun)
    {
        if (!_containers.TryGetContainer(gun, "upgrades", out var container) || container.ContainedEntities.Count == 0)
            return null;

        return container.ContainedEntities[0];
    }

}
