using Content.Server.Fluids.EntitySystems;
using Content.Server.GridPreloader;
using Content.Shared._Onyx.Salvage.Shelter;
using Content.Shared.Chemistry.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Shelter;

public sealed partial class DeployableGridSystem : SharedDeployableGridSystem
{
    [Dependency] private GridPreloaderSystem _preloader = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeployableGridComponent, DeployableGridDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(Entity<DeployableGridComponent> ent, ref DeployableGridDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !TryDeploy(ent))
            return;
        args.Handled = true;
        QueueDel(ent);
    }

    private bool TryDeploy(Entity<DeployableGridComponent> ent)
    {
        var xform = Transform(ent);
        if (TerminatingOrDeleted(ent) || !CheckCanDeploy(ent) || xform.MapUid == null)
            return false;
        var world = _transform.GetMapCoordinates(ent, xform);
        var destination = new EntityCoordinates(xform.MapUid.Value, (world.Position + ent.Comp.Offset).Rounded());
        var smoke = Spawn("Smoke", world);
        _smoke.StartSmoke(smoke, new Solution(), ent.Comp.DeployTime + 2f, (int) Math.Round(ent.Comp.BoxSize.Length() * 2));
        if (_preloader.TryGetPreloadedGrid(ent.Comp.PreloadedGrid, out var preloaded))
        {
            Place(preloaded.Value, destination);
            return true;
        }
        _map.CreateMap(out var dummy);
        if (!_loader.TryLoadGrid(dummy, _prototypes.Index(ent.Comp.PreloadedGrid).Path, out var loaded))
        {
            _map.DeleteMap(dummy);
            return false;
        }
        Place(loaded.Value.Owner, destination);
        _map.DeleteMap(dummy);
        return true;
    }

    private void Place(Entity<TransformComponent?> grid, EntityCoordinates coords)
    {
        if (Resolve(grid, ref grid.Comp))
            _transform.SetCoordinates(grid, grid.Comp, coords, Angle.Zero);
    }
}
