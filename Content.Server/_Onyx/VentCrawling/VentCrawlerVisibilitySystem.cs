using Content.Shared._Onyx.VentCrawling;
using Content.Shared.Eye;

namespace Content.Server._Onyx.VentCrawling;

public sealed partial class VentCrawlerVisibilitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VentCrawlerComponent, GetVisMaskEvent>(OnGetVisibility);
    }

    private void OnGetVisibility(Entity<VentCrawlerComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.InTube)
            args.VisibilityMask |= (int) VisibilityFlags.Subfloor;
    }
}
