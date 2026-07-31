using Content.Shared.Light.Components;
using Robust.Shared.Map;

namespace Content.Shared._Onyx.Weather;

public sealed class GridRoofSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInitialize);
    }

    private void OnGridInitialize(GridInitializeEvent args)
    {
        EnsureComp<RoofComponent>(args.EntityUid);
    }
}
