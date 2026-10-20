// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.Wall;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using ContentDrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Onyx.Overlays;

/// <summary>Evaluates whole-footprint visibility on the GPU using the current viewport's FOV.</summary>
public sealed partial class EntityOcclusionOverlay : Overlay
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private ILightManager _light = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    internal const string PostShaderId = "entity-occlusion";
    private static readonly ProtoId<ShaderPrototype> FootprintShader = "EntityOcclusionFootprint";
    private static readonly ProtoId<ShaderPrototype> OcclusionShader = "EntityOcclusion";
    private static readonly ProtoId<ShaderPrototype> ReductionShader = "EntityOcclusionReduce";
    private readonly SpriteSystem _sprite;
    private readonly SpriteTreeSystem _trees;
    private readonly SharedTransformSystem _transform;
    private readonly SharedPhysicsSystem _physics;
    private readonly EntityQuery<FixturesComponent> _fixtures;
    private readonly EntityQuery<MapComponent> _maps;
    private readonly EntityQuery<WallMountComponent> _wallMounts;
    private readonly EntityQuery<OccluderComponent> _occluders;
    private HashSet<Entity<SpriteComponent, TransformComponent>> _candidates = [];
    private readonly List<EntityOcclusionFootprint> _footprints = [];
    private readonly Dictionary<EntityUid, ShaderInstance> _attached = [];
    private readonly Dictionary<EntityUid, int> _rows = [];
    private readonly List<EntityUid> _removed = [];
    private readonly List<FootprintPass> _passes = [];
    private readonly List<ShaderInstance> _reductions = [];
    private IRenderTexture? _mask;
    private IRenderTexture? _maskB;
    private IRenderTexture? _result;
    private IClydeViewport? _viewport;
    private Vector2 _eye;
    private Vector2 _worldOrigin;
    private Vector2 _worldHorizontal;
    private Vector2 _worldVertical;
    private int _passCount;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public EntityOcclusionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entities.System<SpriteSystem>();
        _trees = _entities.System<SpriteTreeSystem>();
        _transform = _entities.System<SharedTransformSystem>();
        _physics = _entities.System<SharedPhysicsSystem>();
        _fixtures = _entities.GetEntityQuery<FixturesComponent>();
        _maps = _entities.GetEntityQuery<MapComponent>();
        _wallMounts = _entities.GetEntityQuery<WallMountComponent>();
        _occluders = _entities.GetEntityQuery<OccluderComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _viewport = null;
        _result = null;
        _rows.Clear();
        if (args.Viewport.Eye is not { } eye || !_light.Enabled || !_light.DrawHardFov ||
            !eye.DrawLight || !eye.DrawFov || !_maps.TryComp(args.MapUid, out var map) || !map.LightingEnabled)
        {
            RestoreAll();
            return;
        }
        _candidates.Clear();
        foreach (var (uid, tree) in _trees.GetIntersectingTrees(args.MapId, args.WorldBounds))
        {
            var bounds = _transform.GetInvWorldMatrix(uid).TransformBox(args.WorldBounds);
            tree.Tree.QueryAabb(ref _candidates,
                static (ref HashSet<Entity<SpriteComponent, TransformComponent>> sprites,
                    in ComponentTreeEntry<SpriteComponent> entry) =>
                {
                    sprites.Add(entry);
                    return true;
                }, bounds, false);
        }
        _passCount = 0;
        var localEntity = _player.LocalEntity;
        foreach (var ent in _candidates)
        {
            if (ent.Owner == localEntity || ent.Comp1.DrawDepth <= (int) ContentDrawDepth.WallTops ||
                _wallMounts.HasComp(ent.Owner) || IsOccludingSurface(ent.Owner))
                continue;
            var row = _rows.Count;
            _rows.Add(ent.Owner, row);
            _footprints.Clear();
            CollectFootprints(ent.Owner, ent.Comp2, _footprints);
            foreach (var footprint in _footprints)
            {
                if (_passCount == _passes.Count)
                    _passes.Add(new FootprintPass(_prototypes.Index(FootprintShader).InstanceUnique()));
                var pass = _passes[_passCount++];
                pass.Row = row;
                pass.SetFootprint(footprint, eye.Position.Position);
            }
            if (_attached.ContainsKey(ent.Owner))
                continue;
            var shader = _prototypes.Index(OcclusionShader).InstanceUnique();
            _attached.Add(ent.Owner, shader);
            _sprite.SetPostShader(ent.Comp1, new SpriteComponent.PostShaderArgs(PostShaderId, shader)
            {
                RaiseShaderEvent = true,
                After = ContentPostShaderIds.AfterBaseEffects,
                Before = ContentPostShaderIds.BeforeOutlines,
            });
        }
        RemoveUnused();
        if (_rows.Count == 0)
            return;
        var width = args.Viewport.FovRenderTarget.Size.X;
        if (_mask == null || _mask.Size.Y < _rows.Count || _mask.Size.X != width)
        {
            _mask?.Dispose();
            _maskB?.Dispose();
            _mask = _clyde.CreateRenderTarget(new Vector2i(width, (int) BitOperations.RoundUpToPowerOf2((uint) _rows.Count)),
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8), new TextureSampleParameters { Filter = false });
            _maskB = _clyde.CreateRenderTarget(_mask.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8), new TextureSampleParameters { Filter = false });
        }
        var handle = args.RenderHandle.DrawingHandleScreen;
        var depth = args.Viewport.FovRenderTarget;
        handle.RenderInRenderTarget(_mask, () =>
        {
            handle.SetTransform(Matrix3x2.Identity);
            for (var i = 0; i < _passCount; i++)
            {
                var pass = _passes[i];
                pass.Shader.SetParameter("fovDepth", depth.Texture);
                pass.Shader.SetParameter("mapWidth", (float) depth.Size.X);
                handle.UseShader(pass.Shader);
                handle.DrawTextureRect(Texture.White, new UIBox2(0, pass.Row, width, pass.Row + 1));
            }
        }, Color.Transparent);
        var source = _mask;
        var target = _maskB!;
        var level = 0;
        for (var count = width; count > 1; count = (count + 1) / 2)
        {
            if (level == _reductions.Count)
                _reductions.Add(_prototypes.Index(ReductionShader).InstanceUnique());
            var shader = _reductions[level++];
            shader.SetParameter("sourceMask", source.Texture);
            shader.SetParameter("sourceSize", (Vector2) source.Size);
            shader.SetParameter("sourceWidth", (float) count);
            var outputWidth = (count + 1) / 2;
            handle.RenderInRenderTarget(target, () =>
            {
                handle.SetTransform(Matrix3x2.Identity);
                handle.UseShader(shader);
                handle.DrawTextureRect(Texture.White, new UIBox2(0, 0, outputWidth, _rows.Count));
            }, Color.Transparent);
            (source, target) = (target, source);
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        _result = source;
        _viewport = args.Viewport;
        _eye = eye.Position.Position;
        var origin = _viewport.LocalToWorld(new Vector2(0f, _viewport.Size.Y)).Position;
        _worldOrigin = origin - _eye;
        _worldHorizontal = _viewport.LocalToWorld((Vector2) _viewport.Size).Position - origin;
        _worldVertical = _viewport.LocalToWorld(Vector2.Zero).Position - origin;
    }

    internal void ConfigureShader(Entity<SpriteComponent> ent, in BeforePostShaderRenderEvent args)
    {
        var hasRow = _rows.TryGetValue(ent.Owner, out var row);
        var enabled = args.Viewport == _viewport && _result != null && hasRow;
        args.Shader.SetParameter("enabled", enabled);
        if (!enabled)
            return;
        args.Shader.SetParameter("visibilityMask", _result!.Texture);
        args.Shader.SetParameter("fovDepth", args.Viewport.FovRenderTarget.Texture);
        args.Shader.SetParameter("worldOrigin", _worldOrigin);
        args.Shader.SetParameter("worldHorizontal", _worldHorizontal);
        args.Shader.SetParameter("worldVertical", _worldVertical);
        args.Shader.SetParameter("anchor", _transform.GetWorldPosition(ent.Owner) - _eye);
        args.Shader.SetParameter("maskUV", GetMaskUv(_result.Size, row));
    }

    internal static Vector2 GetMaskUv(Vector2i size, int row) =>
        new(0.5f / size.X, 1f - (row + 0.5f) / size.Y);

    // FOV already draws occluding surfaces up to their far face; a near-face cut would hide the surface itself.
    internal bool IsOccludingSurface(EntityUid uid) =>
        _occluders.TryComp(uid, out var occluder) && occluder.Enabled;

    internal void CollectFootprints(EntityUid uid, TransformComponent xform, List<EntityOcclusionFootprint> output)
    {
        var start = output.Count;
        if (_fixtures.TryComp(uid, out var fixtures))
        {
            var transform = _physics.GetPhysicsTransform(uid, xform);
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Hard)
                    EntityOcclusionFootprint.Append(fixture.Shape, transform, output);
            }
        }
        if (output.Count == start)
            output.Add(new EntityOcclusionFootprint(_transform.GetWorldPosition(xform), 0f));
    }

    private void RemoveUnused()
    {
        _removed.Clear();
        foreach (var uid in _attached.Keys)
        {
            if (!_rows.ContainsKey(uid))
                _removed.Add(uid);
        }
        foreach (var uid in _removed)
        {
            _sprite.RemovePostShader((uid, null), PostShaderId);
            _attached.Remove(uid, out var shader);
            shader?.Dispose();
        }
    }

    /// <summary>Removes this overlay's post-shaders without changing sprite color or other effects.</summary>
    public void RestoreAll()
    {
        _viewport = null;
        _result = null;
        _rows.Clear();
        RemoveUnused();
    }

    protected override void DisposeBehavior()
    {
        RestoreAll();
        _mask?.Dispose();
        _maskB?.Dispose();
        foreach (var shader in _reductions)
            shader.Dispose();
        foreach (var pass in _passes)
            pass.Shader.Dispose();
        base.DisposeBehavior();
    }

    private sealed class FootprintPass(ShaderInstance shader)
    {
        public readonly ShaderInstance Shader = shader;
        public int Row;
        private readonly Vector2[] _vertices = new Vector2[PhysicsConstants.MaxPolygonVertices];

        public void SetFootprint(EntityOcclusionFootprint footprint, Vector2 eye)
        {
            var vertices = footprint.Vertices;
            Shader.SetParameter("circle", vertices == null);
            if (vertices == null)
            {
                Shader.SetParameter("center", footprint.Center - eye);
                Shader.SetParameter("radius", footprint.Radius);
                return;
            }
            for (var i = 0; i < vertices.Length; i++)
                _vertices[i] = vertices[i] - eye;
            Shader.SetParameter("vertices", _vertices);
            Shader.SetParameter("vertexCount", vertices.Length);
        }
    }
}
