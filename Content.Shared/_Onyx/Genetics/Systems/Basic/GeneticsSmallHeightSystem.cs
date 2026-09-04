// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Genetics.Components;
using Content.Shared.Humanoid;

namespace Content.Shared.Genetics.Systems;

public sealed partial class GeneticsSmallHeightSystem : EntitySystem
{
    [Dependency] private HumanoidProfileSystem _humanoid = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticsSmallHeightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GeneticsSmallHeightComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<GeneticsSmallHeightComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<HumanoidProfileComponent>(ent, out var profile))
            return;

        ent.Comp.PreviousHeight = profile.Height;
        _humanoid.SetHeight((ent, profile), 140f / 175f);
        Dirty(ent);
    }

    private void OnShutdown(Entity<GeneticsSmallHeightComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.PreviousHeight is not { } height || !TryComp<HumanoidProfileComponent>(ent, out var profile))
            return;

        _humanoid.SetHeight((ent, profile), height);
    }
}
