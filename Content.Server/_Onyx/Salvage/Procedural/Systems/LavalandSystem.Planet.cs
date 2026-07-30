using System.Numerics;
using Content.Server._Onyx.Salvage.DeathRattle;
using Content.Server._Onyx.Salvage.Procedural.Components;
using Content.Shared._Onyx.Salvage.Procedural.Prototypes;
using Content.Shared.Atmos.Components;
using Content.Shared.Gravity;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Salvage;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Procedural.Systems;

public sealed partial class LavalandSystem
{
    public bool SetupLavalandPlanet(
        ProtoId<LavalandMapPrototype> mapPrototype,
        Entity<LavalandPreloaderComponent> preloader,
        out Entity<LavalandPlanetComponent>? lavaland,
        int? seed = null)
    {
        lavaland = null;
        if (!_enabled || HasLavaland())
            return false;

        var mapConfig = _prototypes.Index(mapPrototype);
        var planet = _prototypes.Index(mapConfig.Planet);
        var layout = _prototypes.Index(mapConfig.Layout);
        var ruins = _prototypes.Index(mapConfig.Ruins);
        var mapUid = _map.CreateMap(out var mapId, runMapInit: false);

        try
        {
            EnsureComp<LavalandMapComponent>(mapUid);
            var state = EnsureComp<LavalandPlanetComponent>(mapUid);
            lavaland = (mapUid, state);
            state.Seed = seed ?? _random.Next();
            state.Prototype = mapPrototype;

            SetupPlanet(mapUid, planet, state.Seed);
            // ponytail: Warm the fixed arrival area; expand this if the Lavaland outpost moves away from the origin.
            EnsureComp<LavalandBiomeOptimizationComponent>(mapUid).WarmupArea = new Box2(-64, -64, 64, 64);
            _map.SetPaused(mapId, true);
            LoadLayout((mapUid, state), mapId, layout);
            SetupRuins(ruins, (mapUid, state), preloader);

            foreach (var grid in _map.GetAllGrids(mapId))
                _shuttle.AddIFFFlag(grid, IFFFlags.HideLabel);

            if (planet.AddComponents != null)
                EntityManager.AddComponents(mapUid, planet.AddComponents);
            _map.InitializeMap(mapId);
            return true;
        }
        catch
        {
            Del(mapUid);
            lavaland = null;
            throw;
        }
    }

    private void SetupPlanet(EntityUid mapUid, LavalandPlanetPrototype planet, int seed)
    {
        _metadata.SetEntityName(mapUid, Loc.GetString(planet.Name));
        _biome.EnsurePlanet(mapUid, _prototypes.Index(planet.Biome), seed, mapLight: planet.MapLight);
        var biome = EnsureComp<BiomeComponent>(mapUid);
        foreach (var marker in planet.MarkerLayers)
            _biome.AddMarkerLayer(mapUid, biome, marker);
        Dirty(mapUid, biome);

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        Dirty(mapUid, gravity);
        var atmosphere = EnsureComp<MapAtmosphereComponent>(mapUid);
        _atmosphere.SetMapGasMixture(mapUid, planet.Atmosphere, atmosphere);
        EnsureComp<RestrictedRangeComponent>(mapUid).Range = planet.RestrictedRange;
    }

    private void LoadLayout(
        Entity<LavalandPlanetComponent> lavaland,
        MapId mapId,
        LavalandLayoutPrototype layout)
    {
        foreach (var entry in layout.Layouts)
        {
            if (!_mapLoader.TryLoadGrid(mapId, entry.GridPath, out var loaded))
                throw new InvalidOperationException($"Failed to load required Lavaland layout grid '{entry.GridPath}'.");

            _transform.SetCoordinates(loaded.Value.Owner, new EntityCoordinates(lavaland, entry.Position));
            _metadata.SetEntityName(loaded.Value.Owner, Loc.GetString(entry.Name));
            lavaland.Comp.LayoutGrids.Add(loaded.Value.Owner);
        }
    }
}
