using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Server.Maps;

/// <summary>
///     Performs basic map migration operations by listening for engine <see cref="MapLoaderSystem"/> events.
/// </summary>
public sealed partial class MapMigrationSystem : EntitySystem
{
    [Dependency] private IResourceManager _resMan = default!;
    [Dependency] private SharedMapSystem _map = default!; // <Onyx-TileVariantMigration>
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!; // <Onyx-TileVariantMigration>

    private const string MigrationFile = "/migration.yml";
    // <Onyx-LavalandMigration>
    private readonly Dictionary<string, string?> _migrations = new();
    // </Onyx-LavalandMigration>

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeforeEntityReadEvent>(OnBeforeReadEvent);
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit); // <Onyx-TileVariantMigration>

        // <Onyx-LavalandMigration>
        if (!TryReadFile(out var mappings))
            return;

        foreach (var (key, value) in mappings)
        {
            if (value is not ValueDataNode valueNode)
                continue;

            _migrations[key] = string.IsNullOrWhiteSpace(valueNode.Value) || valueNode.Value == "null"
                ? null
                : valueNode.Value;
        }
        // </Onyx-LavalandMigration>

#if DEBUG
        // <Onyx-LavalandMigration-edited>
        // Verify that all of the entries map to valid entity prototypes.
        foreach (var newId in _migrations.Values)
        {
            if (newId != null)
                DebugTools.Assert(ProtoMan.HasIndex<EntityPrototype>(newId), $"{newId} is not an entity prototype.");
        }
        // </Onyx-LavalandMigration-edited>
#endif
    }

    private bool TryReadFile([NotNullWhen(true)] out MappingDataNode? mappings)
    {
        mappings = null;
        var path = new ResPath(MigrationFile);
        if (!_resMan.TryContentFileRead(path, out var stream))
            return false;

        using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
        var documents = DataNodeParser.ParseYamlStream(reader).FirstOrDefault();

        if (documents == null)
            return false;

        mappings = (MappingDataNode) documents.Root;
        return true;
    }

    private void OnBeforeReadEvent(BeforeEntityReadEvent ev)
    {
        // <Onyx-LavalandMigration-edited>
        foreach (var (oldId, newId) in _migrations)
        {
            if (newId == null)
                ev.DeletedPrototypes.Add(oldId);
            else
                ev.RenamedPrototypes.Add(oldId, newId);
        }
        // </Onyx-LavalandMigration-edited>
    }

    // <Onyx-TileVariantMigration>
    private void OnGridInit(GridInitializeEvent args)
    {
        List<(Vector2i GridIndices, Tile Tile)>? replacements = null;
        foreach (var tileRef in _map.GetAllTiles(args.EntityUid, args.Grid))
        {
            var tile = tileRef.Tile;
            if (tile.Variant < _tileDefinitions[tile.TypeId].Variants)
                continue;

            replacements ??= new();
            replacements.Add((tileRef.GridIndices,
                new Tile(tile.TypeId, tile.Flags, variant: 0, tile.RotationMirroring)));
        }

        if (replacements != null)
            _map.SetTiles(args.EntityUid, args.Grid, replacements);
    }
    // </Onyx-TileVariantMigration>

    // <Onyx-LavalandMigration>
    public bool TryMigrateEntityPrototype(string prototype, [NotNullWhen(true)] out string? migrated)
    {
        migrated = prototype;
        var visited = new HashSet<string>();
        while (_migrations.TryGetValue(migrated, out var next))
        {
            if (next == null || !visited.Add(migrated))
            {
                migrated = null;
                return false;
            }

            migrated = next;
        }

        return true;
    }
    // </Onyx-LavalandMigration>
}
