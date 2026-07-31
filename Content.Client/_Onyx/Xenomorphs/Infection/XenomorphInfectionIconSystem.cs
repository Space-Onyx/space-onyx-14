using Content.Shared._Onyx.Xenomorphs.Infection;
using Content.Shared._Onyx.Xenomorphs.Larva;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Xenomorphs.Infection;

public sealed partial class XenomorphInfectionIconSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenomorphInfectedComponent, GetStatusIconsEvent>(OnInfectedIcons);
        SubscribeLocalEvent<XenomorphLarvaVictimComponent, GetStatusIconsEvent>(OnLarvaIcon);
    }

    private void OnInfectedIcons(Entity<XenomorphInfectedComponent> ent, ref GetStatusIconsEvent args)
    {
        if (ent.Comp.InfectedIcons.TryGetValue(ent.Comp.GrowthStage, out var id) &&
            _prototypes.TryIndex(id, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnLarvaIcon(Entity<XenomorphLarvaVictimComponent> ent, ref GetStatusIconsEvent args)
    {
        if (ent.Comp.InfectedIcon is { } id && _prototypes.TryIndex(id, out var icon))
            args.StatusIcons.Add(icon);
    }
}
