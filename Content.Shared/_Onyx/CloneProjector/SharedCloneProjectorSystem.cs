using System.Linq;
using Content.Shared._Onyx.Holograms;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._Onyx.CloneProjector;

public abstract partial class SharedCloneProjectorSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HolographicCloneComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HolographicCloneComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<HolographicCloneComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnStartup(Entity<HolographicCloneComponent> clone, ref ComponentStartup args)
    {
        EnsureComp<HologramVisualsComponent>(clone);
    }

    private void OnMeleeHit(Entity<HolographicCloneComponent> clone, ref MeleeHitEvent args)
    {
        if (!args.IsHit || clone.Comp.HostEntity is not { } host)
            return;

        if (args.HitEntities.Contains(host))
            args.BonusDamage = -args.BaseDamage;
    }

    private void OnShotAttempted(Entity<HolographicCloneComponent> clone, ref ShotAttemptedEvent args)
    {
        if (clone.Comp.HostProjector is not { } projector || !projector.Comp.RestrictRangedWeapons)
            return;

        _popup.PopupClient(Loc.GetString("gun-disabled"), clone, clone);
        args.Cancel();
    }
}
