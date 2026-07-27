using Content.Server._Onyx.Salvage.DeathRattle;
using Content.Shared.CrusherUpgrades;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server.CrusherUpgrades;

public sealed partial class MapRestrictedSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<MapRestrictedGunComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnAttemptShoot(Entity<MapRestrictedGunComponent> ent, ref AttemptShootEvent args)
    {
        if (!HasComp<MapRestrictedComponent>(ent) ||
            Transform(ent).MapUid is { } map && HasComp<LavalandMapComponent>(map))
            return;

        args.Cancelled = true;
        if (ent.Comp.PopupOnBlock is { } text)
            args.Message = Loc.GetString(text);
    }
}
