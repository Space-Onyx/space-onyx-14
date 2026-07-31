using System.Linq;
using Content.Server._Onyx.Projectiles.TargetGuided;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Server._Onyx.FireControl;

public sealed partial class FireControlSystem
{
    private static readonly TimeSpan GuidanceTimeout = TimeSpan.FromSeconds(1);

    [Dependency] private TargetGuidedSystem _targetGuided = default!;

    private readonly Dictionary<EntityUid, (EntityUid Console, EntityCoordinates Target)> _guidedShots = new();
    private readonly HashSet<EntityUid> _guidedMissiles = new();

    private void InitializeTargetGuided()
    {
        SubscribeLocalEvent<GunComponent, AmmoShotEvent>(OnGuidedShot);
        SubscribeLocalEvent<TargetGuidedComponent, ComponentShutdown>(OnGuidedShutdown);
    }

    private void FireGuided(EntityUid console, EntityUid weapon, Entity<GunComponent> gun, EntityCoordinates target)
    {
        _guidedShots[weapon] = (console, target);
        try
        {
            _gun.AttemptShoot(weapon, gun, target);
        }
        finally
        {
            _guidedShots.Remove(weapon);
        }
    }

    private void OnGuidedShot(Entity<GunComponent> gun, ref AmmoShotEvent args)
    {
        if (!_guidedShots.TryGetValue(gun.Owner, out var shot))
            return;

        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp<TargetGuidedComponent>(projectile, out var guided) &&
                _targetGuided.SetTarget((projectile, guided), shot.Console, shot.Target))
                _guidedMissiles.Add(projectile);
        }
    }

    private void UpdateTargetGuided()
    {
        foreach (var missile in _guidedMissiles.ToArray())
        {
            if (!TryComp<TargetGuidedComponent>(missile, out var guided) ||
                guided.FixedDirection != null ||
                guided.ControllingConsole is not { } console ||
                !_consoleTargets.TryGetValue(console, out var target) ||
                _timing.CurTime - target.Updated > GuidanceTimeout ||
                !_targetGuided.SetTarget((missile, guided), console, target.Target))
                _guidedMissiles.Remove(missile);
        }

        foreach (var (console, target) in _consoleTargets.ToArray())
        {
            if (!Exists(console) || _timing.CurTime - target.Updated > GuidanceTimeout)
                _consoleTargets.Remove(console);
        }
    }

    private void OnGuidedShutdown(Entity<TargetGuidedComponent> missile, ref ComponentShutdown args)
    {
        _guidedMissiles.Remove(missile.Owner);
    }
}
