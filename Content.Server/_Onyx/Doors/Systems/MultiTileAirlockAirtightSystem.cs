// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Server._Onyx.Doors.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server._Onyx.Doors.Systems;

public sealed partial class MultiTileAirlockAirtightSystem : EntitySystem
{
    [Dependency] private AirtightSystem _airtight = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MultiTileAirlockAirtightComponent, ComponentStartup>(OnPositionChanged);
        SubscribeLocalEvent<MultiTileAirlockAirtightComponent, AnchorStateChangedEvent>(OnPositionChanged);
        SubscribeLocalEvent<MultiTileAirlockAirtightComponent, ReAnchorEvent>(OnPositionChanged);
        SubscribeLocalEvent<MultiTileAirlockAirtightComponent, MoveEvent>(OnPositionChanged);
        SubscribeLocalEvent<MultiTileAirlockAirtightComponent, AirtightChanged>(OnAirtightChanged);
        SubscribeLocalEvent<MultiTileAirlockAirtightComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnPositionChanged<T>(Entity<MultiTileAirlockAirtightComponent> ent, ref T args)
    {
        SyncBlockers(ent);
    }

    private void OnAirtightChanged(Entity<MultiTileAirlockAirtightComponent> ent, ref AirtightChanged args)
    {
        SyncBlockers(ent);
    }

    private void OnShutdown(Entity<MultiTileAirlockAirtightComponent> ent, ref ComponentShutdown args)
    {
        RemoveBlockers(ent);
    }

    private void SyncBlockers(Entity<MultiTileAirlockAirtightComponent> ent)
    {
        var xform = Transform(ent);
        if (!xform.Anchored || xform.GridUid == null)
        {
            RemoveBlockers(ent);
            return;
        }

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            RemoveBlockers(ent);
            return;
        }

        if (!TryComp<AirtightComponent>(ent, out var parentAirtight))
        {
            RemoveBlockers(ent);
            return;
        }

        RemoveBlockers(ent);

        var anchorTile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

        foreach (var offset in ent.Comp.TileOffsets)
        {
            var rotated = xform.LocalRotation.RotateVec(new Vector2(offset.X, offset.Y));
            var snapped = new Vector2i(
                (int) MathF.Round(rotated.X),
                (int) MathF.Round(rotated.Y));

            var blockerTile = anchorTile + snapped;
            var blockerCoords = _mapSystem.GridTileToLocal(xform.GridUid.Value, grid, blockerTile);
            var blocker = Spawn(ent.Comp.BlockerPrototype, blockerCoords);

            if (TryComp<AirtightComponent>(blocker, out var blockerAirtight))
                _airtight.SetAirblocked((blocker, blockerAirtight), parentAirtight.AirBlocked);

            ent.Comp.BlockerEntities.Add(blocker);
        }
    }

    private void RemoveBlockers(Entity<MultiTileAirlockAirtightComponent> ent)
    {
        foreach (var blocker in ent.Comp.BlockerEntities)
        {
            if (!TerminatingOrDeleted(blocker))
                QueueDel(blocker);
        }
        ent.Comp.BlockerEntities.Clear();
    }
}
