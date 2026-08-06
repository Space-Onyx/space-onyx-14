using Content.Server._Onyx.Gravity.Components;
using Content.Shared._Onyx.Gravity.Components;
using Content.Shared.Gravity;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.Gravity.EntitySystems;

public sealed partial class GridGravityDefaultsSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridGravityDefaultsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GridGravityDefaultsComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
        SubscribeLocalEvent<TransformComponent, MapUidChangedEvent>(OnGridMapChanged);
    }

    private void OnMapInit(Entity<GridGravityDefaultsComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<MapComponent>(ent.Owner, out var map))
            return;

        foreach (var grid in _mapSystem.GetAllGrids(map.MapId))
        {
            if (grid.Owner != ent.Owner)
                ApplyDefaults(grid.Owner, ent);
        }
    }

    private void OnComponentShutdown(Entity<GridGravityDefaultsComponent> ent, ref ComponentShutdown args)
    {
        var query = EntityQueryEnumerator<InheritedGridGravityComponent>();
        while (query.MoveNext(out var gridUid, out var inherited))
        {
            if (inherited.SourceMap == ent.Owner)
                RestoreDefaults(gridUid, inherited);
        }
    }

    private void OnGridInit(GridInitializeEvent args)
    {
        RefreshGrid(args.EntityUid);
    }

    private void OnGridMapChanged(Entity<TransformComponent> ent, ref MapUidChangedEvent args)
    {
        if (!HasComp<MapGridComponent>(ent))
            return;

        RefreshGrid(ent.Owner);
    }

    private void RefreshGrid(EntityUid gridUid)
    {
        var mapUid = Transform(gridUid).MapUid;

        if (mapUid is { } uid && TryComp<GridGravityDefaultsComponent>(uid, out var defaults))
        {
            ApplyDefaults(gridUid, (uid, defaults));
            return;
        }

        RestoreDefaults(gridUid);
    }

    private void ApplyDefaults(EntityUid gridUid, Entity<GridGravityDefaultsComponent> map)
    {
        if (TryComp<InheritedGridGravityComponent>(gridUid, out var inherited))
        {
            if (inherited.SourceMap == map.Owner)
            {
                SetGravity(gridUid, map.Comp.Enabled, map.Comp.Inherent);
                return;
            }

            RestoreDefaults(gridUid, inherited);
        }

        var hadGravity = TryComp<GravityComponent>(gridUid, out var existing);
        inherited = EnsureComp<InheritedGridGravityComponent>(gridUid);
        inherited.SourceMap = map.Owner;
        inherited.HadGravity = hadGravity;

        if (existing != null)
        {
            inherited.PreviousEnabled = existing.Enabled;
            inherited.PreviousInherent = existing.Inherent;
        }

        SetGravity(gridUid, map.Comp.Enabled, map.Comp.Inherent);
    }

    private void SetGravity(EntityUid gridUid, bool enabled, bool inherent)
    {
        var gravity = EnsureComp<GravityComponent>(gridUid);
        gravity.Inherent = inherent;
        gravity.Enabled = enabled;
        Dirty(gridUid, gravity);
    }

    private void RestoreDefaults(
        EntityUid gridUid,
        InheritedGridGravityComponent? inherited = null)
    {
        if (!Resolve(gridUid, ref inherited, false))
            return;

        if (inherited.HadGravity)
        {
            var gravity = EnsureComp<GravityComponent>(gridUid);
            gravity.Inherent = inherited.PreviousInherent;
            gravity.Enabled = inherited.PreviousEnabled;
            Dirty(gridUid, gravity);
        }
        else
        {
            RemComp<GravityComponent>(gridUid);
        }

        RemComp<InheritedGridGravityComponent>(gridUid);
    }
}
