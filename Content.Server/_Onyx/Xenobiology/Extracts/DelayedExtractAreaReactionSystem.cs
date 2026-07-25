using Content.Server.Fluids.EntitySystems;
using Content.Server.Spreader;
using Content.Shared._Onyx.Xenobiology.Extracts;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.EntityEffects;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Xenobiology.Extracts;

public sealed partial class DelayedExtractAreaReactionSystem
    : EntityEffectSystem<SlimeExtractComponent, DelayedExtractAreaReaction>
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SpreaderSystem _spreader = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void Effect(Entity<SlimeExtractComponent> entity, ref EntityEffectEvent<DelayedExtractAreaReaction> args)
    {
        var effect = args.Effect;
        if (effect.Delay < TimeSpan.Zero || effect.SpreadAmount <= 0 ||
            !_solutions.TryGetRefillableSolution(entity.Owner, out _, out var solution))
        {
            return;
        }

        var coordinates = _transform.GetMapCoordinates(entity);
        var contents = solution.Clone();
        Timer.Spawn(effect.Delay, () => SpawnArea(coordinates, contents, effect));
    }

    private void SpawnArea(MapCoordinates mapCoordinates,
        Content.Shared.Chemistry.Components.Solution solution,
        DelayedExtractAreaReaction effect)
    {
        if (!_map.TryFindGridAt(mapCoordinates, out var gridUid, out var grid))
            return;

        var coordinates = _map.MapToGrid(gridUid, mapCoordinates).SnapToGrid();
        if (!_map.TryGetTileRef(gridUid, grid, coordinates, out var tileRef) ||
            _spreader.RequiresFloorToSpread(effect.PrototypeId.ToString()) && _turf.IsSpace(tileRef))
        {
            return;
        }

        var smoke = Spawn(effect.PrototypeId, coordinates);
        _smoke.StartSmoke(smoke, solution, effect.Duration, effect.SpreadAmount);
        _audio.PlayPvs(effect.Sound, smoke, AudioParams.Default.WithVariation(0.25f));
    }
}
