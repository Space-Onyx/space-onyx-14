using Content.Shared._Onyx.Weather;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.Weather;

[RegisterComponent]
public sealed partial class SetTileWeatherComponent : Component
{
    [DataField(required: true)]
    public bool Disable;
}

public sealed partial class TileWeatherSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SetTileWeatherComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<SetTileWeatherComponent> ent, ref ComponentStartup args)
    {
        var xform = Transform(ent);
        if (_gridQuery.TryComp(xform.GridUid, out var grid))
        {
            var gridUid = xform.GridUid.Value;
            var index = _map.LocalToTile(gridUid, grid, xform.Coordinates);
            SetOverride((gridUid, EnsureComp<TileWeatherComponent>(gridUid)), index, ent.Comp.Disable);
        }

        QueueDel(ent);
    }

    private void SetOverride(Entity<TileWeatherComponent> grid, Vector2i index, bool disable)
    {
        var chunk = SharedMapSystem.GetChunkIndices(index, TileWeatherComponent.ChunkSize);
        var relative = SharedMapSystem.GetChunkRelative(index, TileWeatherComponent.ChunkSize);
        var bit = 1UL << (relative.X + relative.Y * TileWeatherComponent.ChunkSize);
        var add = disable ? grid.Comp.Disabled : grid.Comp.Enabled;
        var remove = disable ? grid.Comp.Enabled : grid.Comp.Disabled;

        add[chunk] = add.GetValueOrDefault(chunk) | bit;
        if (remove.TryGetValue(chunk, out var data))
        {
            data &= ~bit;
            if (data == 0)
                remove.Remove(chunk);
            else
                remove[chunk] = data;
        }

        Dirty(grid);
    }
}
