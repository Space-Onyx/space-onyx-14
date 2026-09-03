using Content.Shared.Access.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.StationRecords;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared._Onyx.Surgery.Augments;
using Content.Shared._Onyx.Surgery.Organs;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;

namespace Content.Client._Onyx.Surgery.Augments;

public sealed partial class CyberDeckScriptOverlaySystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ICollection<StationRecordKey> EmptyStationKeys = Array.Empty<StationRecordKey>();
    private const float UpdateInterval = 0.1f;
    private const LookupFlags Lookup = LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.StaticSundries | LookupFlags.Sundries;

    private readonly List<CyberDeckScriptOverlayHelper.HighlightShape> _shapes = new();
    private readonly HashSet<Entity<BodyComponent>> _bodies = new();
    private readonly HashSet<Entity<AirlockComponent>> _airlocks = new();
    private readonly HashSet<Entity<SurveillanceCameraComponent>> _cameras = new();
    private ScriptOverlay? _scriptOverlay;
    private Color _fill;
    private Color _outer;
    private Color _inner;
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        _scriptOverlay = new ScriptOverlay(this);
        _overlay.AddOverlay(_scriptOverlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_scriptOverlay != null)
            _overlay.RemoveOverlay(_scriptOverlay);
        _scriptOverlay = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;
        _accumulator = 0f;
        _shapes.Clear();

        if (CyberDeckScriptOverlayHelper.TryGetActiveScript<CyberDeckScriptRemoteDeactivationComponent>(
                EntityManager, _player, _ui, out var body, out var range, out var remote))
        {
            if (!HasOptics(body))
                return;
            _fill = remote.OverlayFillColor;
            _outer = remote.OverlayOuterOutlineColor;
            _inner = remote.OverlayInnerOutlineColor;
            AddRemoteTargets(body, range, remote);
            return;
        }

        if (CyberDeckScriptOverlayHelper.TryGetActiveScript<CyberDeckScriptOpticsOverloadComponent>(
                EntityManager, _player, _ui, out body, out range, out var optics))
        {
            if (!HasOptics(body))
                return;
            _fill = optics.OverlayFillColor;
            _outer = optics.OverlayOuterOutlineColor;
            _inner = optics.OverlayInnerOutlineColor;
            AddBodyTargets(body, range, HasOptics);
            return;
        }

        if (!CyberDeckScriptOverlayHelper.TryGetActiveScript<CyberDeckScriptMotorImpairmentComponent>(
                EntityManager, _player, _ui, out body, out range, out var motor))
            return;
        if (!HasOptics(body))
            return;

        _fill = motor.OverlayFillColor;
        _outer = motor.OverlayOuterOutlineColor;
        _inner = motor.OverlayInnerOutlineColor;
        AddBodyTargets(body, range, HasMotorics);
    }

    private void AddRemoteTargets(EntityUid body, float range, CyberDeckScriptRemoteDeactivationComponent remote)
    {
        var coordinates = Transform(body).Coordinates;
        _airlocks.Clear();
        _lookup.GetEntitiesInRange(coordinates, range, _airlocks, Lookup);
        foreach (var airlock in _airlocks)
            if (MatchesConfiguredAccess(airlock, remote))
                AddShape(airlock);

        _cameras.Clear();
        _lookup.GetEntitiesInRange(coordinates, range, _cameras, Lookup);
        foreach (var camera in _cameras)
            if (Transform(camera).Anchored)
                AddShape(camera);
    }

    private void AddBodyTargets(EntityUid body, float range, Func<EntityUid, bool> predicate)
    {
        _bodies.Clear();
        _lookup.GetEntitiesInRange(Transform(body).Coordinates, range, _bodies, Lookup);
        foreach (var candidate in _bodies)
            if (candidate.Owner != body && predicate(candidate))
                AddShape(candidate);
    }

    private void AddShape(EntityUid target)
    {
        if (CyberDeckScriptOverlayHelper.TryBuildShape(EntityManager, _transform, target, out var shape))
            _shapes.Add(shape);
    }

    private bool MatchesConfiguredAccess(EntityUid target, CyberDeckScriptRemoteDeactivationComponent remote)
    {
        if (remote.Access.Count == 0)
            return true;
        var matches = true;
        if (_accessReader.GetMainAccessReader(target, out var readerEnt) && readerEnt is { } reader)
            matches = _accessReader.IsAllowed(remote.Access, EmptyStationKeys, reader.Owner, reader.Comp);
        return remote.Inverted ? !matches : matches;
    }

    private bool HasOptics(EntityUid target)
    {
        foreach (var (organ, _) in _body.GetBodyOrgans(target))
            if (HasComp<EyesComponent>(organ) && TryComp(organ, out CyberneticsComponent? cyber) && !cyber.Disabled)
                return true;
        return false;
    }

    private bool HasMotorics(EntityUid target)
    {
        foreach (var (part, partComp) in _body.GetBodyChildren(target))
        {
            if (partComp.PartType != BodyPartType.Leg)
                continue;
            if (TryComp(part, out CyberneticsComponent? cyber) && !cyber.Disabled)
                return true;
            foreach (var (organ, _) in _body.GetPartOrgans(part))
                if (HasComp<AugmentComponent>(organ) || TryComp(organ, out cyber) && !cyber.Disabled)
                    return true;
        }
        return false;
    }

    private sealed class ScriptOverlay : Overlay
    {
        private readonly CyberDeckScriptOverlaySystem _system;

        public override OverlaySpace Space => OverlaySpace.WorldSpace;

        public ScriptOverlay(CyberDeckScriptOverlaySystem system)
        {
            _system = system;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            foreach (var shape in _system._shapes)
                if (shape.Bounds.Intersects(args.WorldAABB))
                    CyberDeckScriptOverlayHelper.Draw(args.WorldHandle, shape, _system._fill, _system._outer, _system._inner);
        }
    }
}
