using System.Numerics;
using System.Linq;
using Content.Shared._Onyx.Radar;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Onyx.Radar;

public sealed partial class RadarBlipSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);
    }

    private void OnBlipsRequested(RequestBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(ev.Radar, out var radar) ||
            !TryComp<RadarConsoleComponent>(radar, out var console) ||
            args.SenderSession.AttachedEntity is not { } actor ||
            !_ui.GetActors(radar.Value, Content.Shared._Onyx.FireControl.FireControlConsoleUiKey.Key).Contains(actor))
            return;

        var radarXform = Transform(radar.Value);
        var radarPosition = _transform.GetWorldPosition(radarXform);
        var radarGrid = radarXform.GridUid;
        var report = new List<(Vector2, float, Color, RadarBlipShape)>();
        var lines = new List<(Vector2, Vector2, float, Color)>();
        var query = EntityQueryEnumerator<RadarBlipComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var blip, out var xform))
        {
            if (!blip.Enabled || xform.MapID != radarXform.MapID)
                continue;

            var grid = xform.GridUid;
            if (blip.RequireNoGrid && grid != null ||
                !blip.RequireNoGrid && !blip.VisibleFromOtherGrids && grid != radarGrid)
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (Vector2.DistanceSquared(position, radarPosition) > console.MaxRange * console.MaxRange)
                continue;

            report.Add((position, blip.Scale, blip.RadarColor, blip.Shape));
        }

        var lineQuery = EntityQueryEnumerator<HitscanRadarComponent, TransformComponent>();
        while (lineQuery.MoveNext(out _, out var line, out var xform))
        {
            if (xform.MapID != radarXform.MapID ||
                Vector2.DistanceSquared(line.Start, radarPosition) > console.MaxRange * console.MaxRange &&
                Vector2.DistanceSquared(line.End, radarPosition) > console.MaxRange * console.MaxRange)
                continue;

            lines.Add((line.Start, line.End, line.Thickness, line.Color));
        }

        RaiseNetworkEvent(new GiveBlipsEvent(report, lines), args.SenderSession);
    }
}
