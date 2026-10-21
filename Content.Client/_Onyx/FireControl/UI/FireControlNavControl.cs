using System.Linq;
using System.Numerics;
using Content.Client._Onyx.Radar;
using Content.Client.Shuttles.UI;
using Content.Shared._Onyx.FireControl;
using Content.Shared._Onyx.Radar;
using Content.Shared.Physics;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.FireControl.UI;

public sealed class FireControlNavControl : BaseShuttleControl
{
    private const float FireInterval = 0.1f;

    private readonly SharedShuttleSystem _shuttles;
    private readonly SharedTransformSystem _transform;
    private readonly SharedPhysicsSystem _physics;
    private readonly RadarBlipsSystem _blips;
    private readonly IGameTiming _timing;

    private EntityCoordinates? _coordinates;
    private EntityUid? _console;
    private Angle? _rotation;
    private bool _mouseDown;
    private bool _mouseInside;
    private Vector2 _mousePosition;
    private TimeSpan _lastFire;
    private List<Entity<MapGridComponent>> _grids = new();
    private FireControllableEntry[] _controllables = [];
    private readonly HashSet<NetEntity> _selectedWeapons = new();
    private readonly Vector2[] _blipPoints = new Vector2[10];
    private readonly List<Vector2> _selectedWeaponPositions = new();

    public Action<EntityCoordinates>? OnRadarClick;

    public FireControlNavControl() : base(64f, 512f, 512f)
    {
        _shuttles = EntManager.System<SharedShuttleSystem>();
        _transform = EntManager.System<SharedTransformSystem>();
        _physics = EntManager.System<SharedPhysicsSystem>();
        _blips = EntManager.System<RadarBlipsSystem>();
        _timing = IoCManager.Resolve<IGameTiming>();
        OnMouseEntered += _ => _mouseInside = true;
        OnMouseExited += _ => _mouseInside = false;
    }

    public void SetConsole(EntityUid console)
    {
        _console = console;
    }

    public void SetSelectedWeapons(IEnumerable<NetEntity> weapons)
    {
        _selectedWeapons.Clear();
        foreach (var weapon in weapons)
            _selectedWeapons.Add(weapon);
    }

    public void SetControllables(FireControllableEntry[] controllables)
    {
        _controllables = controllables;
    }

    public void UpdateState(NavInterfaceState state)
    {
        _coordinates = EntManager.GetCoordinates(state.Coordinates);
        _rotation = state.Angle;
        WorldMaxRange = state.MaxRange;
        if (WorldMaxRange < WorldMinRange)
            WorldMinRange = WorldMaxRange;
        ActualRadarRange = Math.Clamp(ActualRadarRange, WorldMinRange, WorldMaxRange);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _mousePosition = args.RelativePosition;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _mouseDown = true;
        _mousePosition = args.RelativePosition;
        Fire();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _mouseDown = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_console is { } console)
            _blips.RequestBlips(console);
        if (_mouseDown && _mouseInside && _timing.CurTime - _lastFire >= TimeSpan.FromSeconds(FireInterval))
            Fire();
    }

    private void Fire()
    {
        if (_coordinates == null || _rotation == null)
            return;

        _lastFire = _timing.CurTime;
        var local = (_mousePosition * UIScale - MidPointVector) / MinimapScale;
        var world = _rotation.Value.RotateVec(new Vector2(local.X, -local.Y));
        OnRadarClick?.Invoke(_coordinates.Value.Offset(world));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        DrawBacking(handle);
        DrawCircles(handle);

        if (_coordinates == null || _rotation == null ||
            !EntManager.TryGetComponent<TransformComponent>(_coordinates.Value.EntityId, out var xform) ||
            xform.MapID == MapId.Nullspace)
            return;

        var mapPosition = _transform.ToMapCoordinates(_coordinates.Value);
        var positionMatrix = Matrix3Helpers.CreateTransform(_coordinates.Value.Position, _rotation.Value);
        var entityMatrix = Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(xform), _transform.GetWorldRotation(xform));
        Matrix3x2.Invert(positionMatrix * entityMatrix, out var worldToShuttle);
        var shuttleToView = Matrix3x2.CreateScale(MinimapScale, -MinimapScale) * Matrix3x2.CreateTranslation(MidPointVector);
        var worldToView = worldToShuttle * shuttleToView;
        var fixtures = EntManager.GetEntityQuery<FixturesComponent>();
        var bodies = EntManager.GetEntityQuery<PhysicsComponent>();

        if (xform.GridUid is { } ownGrid && EntManager.TryGetComponent<MapGridComponent>(ownGrid, out var ownMapGrid) && fixtures.HasComponent(ownGrid))
            DrawGrid(handle, _transform.GetWorldMatrix(ownGrid) * worldToView, (ownGrid, ownMapGrid), _shuttles.GetIFFColor(ownGrid, true));

        _grids.Clear();
        Maps.FindGridsIntersecting(xform.MapID,
            Box2.CenteredAround(mapPosition.Position, new Vector2(WorldRange * 2f)), ref _grids, approx: true, includeMap: false);
        foreach (var grid in _grids)
        {
            if (grid.Owner == xform.GridUid || !fixtures.HasComponent(grid.Owner) || !bodies.TryGetComponent(grid.Owner, out var body))
                continue;

            EntManager.TryGetComponent<IFFComponent>(grid.Owner, out var iff);
            if (!_shuttles.CanDraw(grid.Owner, body, iff))
                continue;
            DrawGrid(handle, _transform.GetWorldMatrix(grid.Owner) * worldToView, grid, _shuttles.GetIFFColor(grid, false, iff));
        }

        _selectedWeaponPositions.Clear();
        if (_mouseInside)
        {
            foreach (var controllable in _controllables)
            {
                if (_selectedWeapons.Contains(controllable.NetEntity))
                    _selectedWeaponPositions.Add(_transform.ToMapCoordinates(EntManager.GetCoordinates(controllable.Coordinates)).Position);
            }
        }

        foreach (var blip in _blips.GetCurrentBlips())
        {
            DrawBlip(handle, Vector2.Transform(blip.Position, worldToView), blip.Scale, blip.Color, blip.Shape);
            DrawAimTracer(handle, blip.Position, blip.Color, worldToView, xform.MapID);
        }
        foreach (var line in _blips.GetCurrentLines())
            handle.DrawLine(Vector2.Transform(line.Start, worldToView), Vector2.Transform(line.End, worldToView), line.Color);
    }

    private void DrawAimTracer(DrawingHandleScreen handle, Vector2 weaponPosition, Color color, Matrix3x2 worldToView, MapId mapId)
    {
        if (!_mouseInside || _coordinates == null)
            return;

        var selected = false;
        foreach (var position in _selectedWeaponPositions)
        {
            if (Vector2.Distance(position, weaponPosition) < 0.1f)
            {
                selected = true;
                break;
            }
        }
        if (!selected)
            return;

        var cursorViewPosition = _mousePosition * UIScale;
        if (!Matrix3x2.Invert(worldToView, out var viewToWorld))
            return;

        var direction = Vector2.Transform(cursorViewPosition, viewToWorld) - weaponPosition;
        if (direction.LengthSquared() <= 0f)
            return;

        var ray = new CollisionRay(weaponPosition, Vector2.Normalize(direction), (int) CollisionGroup.Impassable);
        if (_physics.IntersectRay(mapId, ray, direction.Length(), ignoredEnt: _coordinates.Value.EntityId).Any())
            return;

        handle.DrawLine(Vector2.Transform(weaponPosition, worldToView), cursorViewPosition, color.WithAlpha(0.3f));
    }

    private void DrawBlip(DrawingHandleScreen handle, Vector2 position, float scale, Color color, RadarBlipShape shape)
    {
        var size = shape == RadarBlipShape.Ring ? scale * MinimapScale : scale * 3f;
        if (shape is RadarBlipShape.Circle or RadarBlipShape.Ring)
        {
            handle.DrawCircle(position, size, color.WithAlpha(0.8f), shape != RadarBlipShape.Ring);
            return;
        }

        var count = shape switch
        {
            RadarBlipShape.Triangle => 3,
            RadarBlipShape.Square or RadarBlipShape.Diamond or RadarBlipShape.Arrow => 4,
            RadarBlipShape.Hexagon => 6,
            RadarBlipShape.Star => 10,
            _ => 0,
        };
        var points = _blipPoints.AsSpan(0, count);
        switch (shape)
        {
            case RadarBlipShape.Square:
                points[0] = new Vector2(-size, -size);
                points[1] = new Vector2(size, -size);
                points[2] = new Vector2(size, size);
                points[3] = new Vector2(-size, size);
                break;
            case RadarBlipShape.Triangle:
                points[0] = new Vector2(0, -size);
                points[1] = new Vector2(size, size);
                points[2] = new Vector2(-size, size);
                break;
            case RadarBlipShape.Diamond:
                points[0] = new Vector2(0, -size);
                points[1] = new Vector2(size, 0);
                points[2] = new Vector2(0, size);
                points[3] = new Vector2(-size, 0);
                break;
            case RadarBlipShape.Arrow:
                points[0] = new Vector2(0, -size);
                points[1] = new Vector2(size / 2f, size);
                points[2] = new Vector2(0, size / 2f);
                points[3] = new Vector2(-size / 2f, size);
                break;
            case RadarBlipShape.Hexagon:
                for (var i = 0; i < count; i++)
                    points[i] = new Angle(i * Math.Tau / 6).ToVec() * size;
                break;
            case RadarBlipShape.Star:
                for (var i = 0; i < count; i++)
                    points[i] = new Angle(i * Math.Tau / 10).ToVec() * (i % 2 == 0 ? size : size * 0.4f);
                break;
        }
        for (var i = 0; i < points.Length; i++)
            points[i] += position;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, color.WithAlpha(0.8f));
    }
}
