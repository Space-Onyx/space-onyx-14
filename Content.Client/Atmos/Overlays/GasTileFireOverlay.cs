using Content.Client.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Species;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client.Atmos.Overlays;

/// <summary>
///     Overlay responsible for rendering atmos fire animation.
/// </summary>
public sealed partial class GasTileFireOverlay : Overlay
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IEntityManager _entManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities | OverlaySpace.WorldSpaceBelowWorld;
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    private readonly SharedTransformSystem _xformSys;
    private readonly SharedMapSystem _mapSystem = default!;
    private readonly ShaderInstance _shader;

    // <Onyx-BurningPuddles-edited>
    private readonly float[,] _timer;
    private readonly float[][][] _frameDelays;
    private readonly int[,] _frameCounter;

    // TODO combine textures into a single texture atlas.
    private readonly Texture[][][] _frames;
    // </Onyx-BurningPuddles-edited>

    private const int FireStates = 3;
    private const string FireRsiPath = "/Textures/Effects/fire.rsi";
    private const string PuddleFireRsiPath = "/Textures/_Onyx/Effects/puddle_fire.rsi"; // <Onyx-BurningPuddles>
    private const int FireTypes = 2; // <Onyx-BurningPuddles>

    public const int GasOverlayZIndex = (int)Shared.DrawDepth.DrawDepth.Effects; // Under ghosts, above mostly everything else

    public GasTileFireOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xformSys = _entManager.System<SharedTransformSystem>();
        _mapSystem = _entManager.System<SharedMapSystem>();
        _shader = _protoMan.Index(UnshadedShader).Instance();
        ZIndex = GasOverlayZIndex;

        // <Onyx-BurningPuddles-edited>
        _timer = new float[FireTypes, FireStates];
        _frameDelays = new float[FireTypes][][];
        _frameCounter = new int[FireTypes, FireStates];
        _frames = new Texture[FireTypes][][];

        var fires = new[]
        {
            _resourceCache.GetResource<RSIResource>(FireRsiPath).RSI,
            _resourceCache.GetResource<RSIResource>(PuddleFireRsiPath).RSI,
        };

        for (var type = 0; type < FireTypes; type++)
        {
            _frames[type] = new Texture[FireStates][];
            _frameDelays[type] = new float[FireStates][];
            for (var stateIndex = 0; stateIndex < FireStates; stateIndex++)
            {
                if (!fires[type].TryGetState((stateIndex + 1).ToString(), out var state))
                    throw new ArgumentOutOfRangeException($"Fire RSI doesn't have state \"{stateIndex + 1}\"!");

                _frames[type][stateIndex] = state.GetFrames(RsiDirection.South);
                _frameDelays[type][stateIndex] = state.GetDelays();
            }
        }
        // </Onyx-BurningPuddles-edited>
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // <Onyx-BurningPuddles-edited>
        for (var type = 0; type < FireTypes; type++)
        {
            for (var state = 0; state < FireStates; state++)
            {
                var delays = _frameDelays[type][state];
                if (delays.Length == 0)
                    continue;

                var frameCount = _frameCounter[type, state];
                _timer[type, state] += args.DeltaSeconds;
                var time = delays[frameCount];

                if (_timer[type, state] < time) continue;
                _timer[type, state] -= time;
                _frameCounter[type, state] = (frameCount + 1) % _frames[type][state].Length;
            }
        }
        // </Onyx-BurningPuddles-edited>
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var drawHandle = args.WorldHandle;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        var overlayQuery = _entManager.GetEntityQuery<GasTileOverlayComponent>();
        var gridState = (args.WorldBounds,
            args.WorldHandle,
            _frames,
            _frameCounter,
            _shader,
            overlayQuery,
            xformQuery,
            _xformSys);

        var mapUid = _mapSystem.GetMapOrInvalid(args.MapId);

        if (args.Space != OverlaySpace.WorldSpaceEntities)
            return;

        // TODO: WorldBounds callback.
        _mapSystem.FindGridsIntersecting(args.MapId, args.WorldAABB, ref gridState,
            static (EntityUid uid, MapGridComponent grid,
                ref (Box2Rotated WorldBounds,
                    DrawingHandleWorld drawHandle,
                    // <Onyx-BurningPuddles-edited>
                    Texture[][][] frames,
                    int[,] frameCounter,
                    // </Onyx-BurningPuddles-edited>
                    ShaderInstance shader,
                    EntityQuery<GasTileOverlayComponent> overlayQuery,
                    EntityQuery<TransformComponent> xformQuery,
                    SharedTransformSystem xformSys) state) =>
            {
                if (!state.overlayQuery.TryGetComponent(uid, out var comp) ||
                    !state.xformQuery.TryGetComponent(uid, out var gridXform))
                {
                    return true;
                }

                var (_, _, worldMatrix, invMatrix) = state.xformSys.GetWorldPositionRotationMatrixWithInv(gridXform);
                state.drawHandle.SetTransform(worldMatrix);
                var floatBounds = invMatrix.TransformBox(state.WorldBounds).Enlarged(grid.TileSize);
                var localBounds = new Box2i(
                    (int)MathF.Floor(floatBounds.Left),
                    (int)MathF.Floor(floatBounds.Bottom),
                    (int)MathF.Ceiling(floatBounds.Right),
                    (int)MathF.Ceiling(floatBounds.Top));

                // Currently it would be faster to group drawing by gas rather than by chunk, but if the textures are
                // ever moved to a single atlas, that should no longer be the case. So this is just grouping draw calls
                // by chunk, even though its currently slower.

                state.drawHandle.UseShader(state.shader);
                foreach (var chunk in comp.Chunks.Values)
                {
                    var enumerator = new GasChunkEnumerator(chunk);

                    while (enumerator.MoveNext(out var gas))
                    {
                        if (gas.FireState == 0)
                            continue;

                        var index = chunk.Origin + (enumerator.X, enumerator.Y);
                        if (!localBounds.Contains(index))
                            continue;

                        var fireState = gas.FireState - 1;
                        var fireType = Math.Min((int) gas.FireType, FireTypes - 1); // <Onyx-BurningPuddles>
                        var texture = state.frames[fireType][fireState][state.frameCounter[fireType, fireState]]; // <Onyx-BurningPuddles-edited>
                        state.drawHandle.DrawTexture(texture, index);
                    }
                }

                return true;
            });

        drawHandle.UseShader(null);
        drawHandle.SetTransform(Matrix3x2.Identity);
    }
}
