// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 ss14-Starlight <ss14-Starlight@outlook.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Atmos.Components;
using Content.Shared.SubFloor;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Onyx.VentCrawling;

public sealed partial class SharedVentTubeSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VentCrawlerBendComponent, GetVentCrawlingsConnectableDirectionsEvent>(OnGetBendDirections);
        SubscribeLocalEvent<VentCrawlerEntryComponent, GetVentCrawlingsConnectableDirectionsEvent>(OnGetEntryDirections);
        SubscribeLocalEvent<VentCrawlerJunctionComponent, GetVentCrawlingsConnectableDirectionsEvent>(OnGetJunctionDirections);
        SubscribeLocalEvent<VentCrawlerTransitComponent, GetVentCrawlingsConnectableDirectionsEvent>(OnGetTransitDirections);
    }

    public EntityUid? NextTubeFor(EntityUid target, Direction nextDirection, AtmosPipeLayer routeLayer, VentCrawlerTubeComponent? targetTube = null)
    {
        if (!Resolve(target, ref targetTube))
            return null;

        var xform = Transform(target);
        if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        if (!SupportsLayer(target, routeLayer))
            return null;

        EntityUid? candidate = null;
        foreach (var entity in _mapSystem.GetInDir(xform.GridUid.Value, grid, xform.Coordinates, nextDirection))
        {
            if (!TryComp(entity, out VentCrawlerTubeComponent? tube)
                || !SupportsLayer(entity, routeLayer)
                || !CanConnect(target, targetTube, nextDirection)
                || !CanConnect(entity, tube, nextDirection.GetOpposite()))
                continue;

            if (candidate != null)
                return null;
            candidate = entity;
        }

        return candidate;
    }

    public AtmosPipeLayer GetLayer(EntityUid uid)
    {
        return TryComp<AtmosPipeLayersComponent>(uid, out var layers)
            ? layers.CurrentPipeLayer
            : AtmosPipeLayer.Primary;
    }

    public bool SupportsLayer(EntityUid uid, AtmosPipeLayer layer)
        => HasComp<VentCrawlerLayerTransitionComponent>(uid) || GetLayer(uid) == layer;

    public bool CanExit(EntityUid target,
        Direction direction,
        AtmosPipeLayer routeLayer,
        out EntityCoordinates exitCoordinates,
        VentCrawlerTubeComponent? targetTube = null)
    {
        exitCoordinates = Transform(target).Coordinates;
        if (!Resolve(target, ref targetTube) || !SupportsLayer(target, routeLayer) || !CanConnect(target, targetTube, direction))
            return false;

        var xform = Transform(target);
        if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return false;

        foreach (var entity in _mapSystem.GetInDir(xform.GridUid.Value, grid, xform.Coordinates, direction))
        {
            if (TryComp(entity, out VentCrawlerTubeComponent? tube) &&
                SupportsLayer(entity, routeLayer) &&
                CanConnect(entity, tube, direction.GetOpposite()))
                return false;

            if (!HasComp<VentCrawlerExitComponent>(entity) ||
                !TryComp<SubFloorHideComponent>(entity, out var exit) ||
                exit.IsUnderCover)
                continue;

            exitCoordinates = Transform(entity).Coordinates;
            return true;
        }

        return TryComp<SubFloorHideComponent>(target, out var subfloor) && !subfloor.IsUnderCover;
    }

    private bool CanConnect(EntityUid uid, VentCrawlerTubeComponent tube, Direction direction)
    {
        if (!tube.Connected)
            return false;

        var ev = new GetVentCrawlingsConnectableDirectionsEvent();
        RaiseLocalEvent(uid, ref ev);
        return ev.Connectable.Contains(direction);
    }

    private void OnGetBendDirections(EntityUid uid, VentCrawlerBendComponent component, ref GetVentCrawlingsConnectableDirectionsEvent args)
    {
        var direction = Transform(uid).LocalRotation;
        args.Connectable = [direction.GetDir(), new Angle(MathHelper.DegreesToRadians(direction.Degrees - 90)).GetDir()];
    }

    private void OnGetEntryDirections(EntityUid uid, VentCrawlerEntryComponent component, ref GetVentCrawlingsConnectableDirectionsEvent args)
        => args.Connectable = [Transform(uid).LocalRotation.GetDir()];

    private void OnGetJunctionDirections(EntityUid uid, VentCrawlerJunctionComponent component, ref GetVentCrawlingsConnectableDirectionsEvent args)
    {
        var direction = Transform(uid).LocalRotation;
        args.Connectable = component.Degrees.Select(degree => new Angle(degree.Theta + direction.Theta).GetDir()).ToArray();
    }

    private void OnGetTransitDirections(EntityUid uid, VentCrawlerTransitComponent component, ref GetVentCrawlingsConnectableDirectionsEvent args)
    {
        var rotation = Transform(uid).LocalRotation;
        args.Connectable = [rotation.GetDir(), new Angle(rotation.Theta + Math.PI).GetDir()];
    }
}
