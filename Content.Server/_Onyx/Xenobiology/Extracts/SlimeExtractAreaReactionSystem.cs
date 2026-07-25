using Content.Server.Fluids.EntitySystems;
using Content.Server.Spreader;
using Content.Shared._Onyx.Xenobiology.Extracts;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Onyx.Xenobiology.Extracts;

public sealed partial class SlimeExtractAreaReactionSystem
    : EntityEffectSystem<SlimeExtractComponent, AreaReactionEffect>
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SpreaderSystem _spreader = default!;
    [Dependency] private TurfSystem _turf = default!;

    protected override void Effect(Entity<SlimeExtractComponent> entity, ref EntityEffectEvent<AreaReactionEffect> args)
    {
        if (!_solutions.TryGetRefillableSolution(entity.Owner, out _, out var solution))
            return;

        var effect = args.Effect;
        var transform = Transform(entity);
        var mapCoordinates = _transform.GetMapCoordinates(entity, transform);
        if (!_map.TryFindGridAt(mapCoordinates, out var gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, transform.Coordinates, out var tileRef) ||
            _spreader.RequiresFloorToSpread(effect.PrototypeId.ToString()) && _turf.IsSpace(tileRef))
        {
            return;
        }

        var coordinates = _map.MapToGrid(gridUid, mapCoordinates).SnapToGrid();
        var smoke = Spawn(effect.PrototypeId, coordinates);
        var spreadAmount = (int) Math.Max(0, Math.Ceiling(args.Scale / effect.OverflowThreshold));
        _smoke.StartSmoke(smoke, solution.Clone(), effect.Duration, spreadAmount);
        _audio.PlayPvs(effect.Sound, entity, AudioParams.Default.WithVariation(0.25f));
    }
}
