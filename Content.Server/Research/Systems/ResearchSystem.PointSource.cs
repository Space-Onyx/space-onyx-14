using Content.Server.Power.EntitySystems;
using Content.Server.Research.Components;
using Content.Shared.Research.Components;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    private void InitializeSource()
    {
        SubscribeLocalEvent<ResearchPointSourceComponent, ResearchServerGetPointsPerSecondEvent>(OnGetPointsPerSecond);
        InitializePointSourceByType(); // <Onyx-ResearchPointTypes>
    }

    private void OnGetPointsPerSecond(Entity<ResearchPointSourceComponent> source, ref ResearchServerGetPointsPerSecondEvent args)
    {
        // <Onyx-ResearchPointTypes>
        // Sources with a custom point type are credited through the by-type event instead.
        if (HasCustomPointType(source))
            return;
        // </Onyx-ResearchPointTypes>

        if (CanProduce(source))
            args.Points += source.Comp.PointsPerSecond;
    }

    public bool CanProduce(Entity<ResearchPointSourceComponent> source)
    {
        return source.Comp.Active && this.IsPowered(source, EntityManager);
    }
}
