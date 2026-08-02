using Content.Shared.Atmos.Components;
using Content.Shared.RCD.Components;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Client.RCD;

public sealed partial class AlignRCDConstruction
{
    [Dependency] private IEyeManager _eyeManager = default!;
    private const float PipeLayerDeadzone = 0.25f;
    private static readonly Color GuideColor = new(0, 0, 0.5785f);
    private const float GuideRadius = 0.1f;
    private const float GuideOffset = 0.21875f;

    public override void Render(in OverlayDrawArgs args)
    {
        var gridId = _transformSystem.GetGrid(MouseCoords);
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (gridId == null ||
            player == null || !_handsSystem.TryGetActiveItem(player.Value, out var held) ||
            !_entityManager.TryGetComponent<RCDComponent>(held, out var rcd) || !rcd.IsRpd ||
            !_entityManager.TryGetComponent<MapGridComponent>(gridId, out var mapGrid))
        {
            base.Render(args);
            return;
        }

        var gridRotation = _transformSystem.GetWorldRotation(gridId.Value);
        var worldPosition = _mapSystem.LocalToWorld(gridId.Value, mapGrid, MouseCoords.Position);
        var direction = (_eyeManager.CurrentEye.Rotation + gridRotation + Math.PI / 2).GetCardinalDir();
        var multi = direction is Direction.North or Direction.South ? -1f : 1f;

        args.WorldHandle.DrawCircle(worldPosition, GuideRadius, GuideColor);
        args.WorldHandle.DrawCircle(worldPosition + gridRotation.RotateVec(new Vector2(multi * GuideOffset, GuideOffset)), GuideRadius, GuideColor);
        args.WorldHandle.DrawCircle(worldPosition - gridRotation.RotateVec(new Vector2(multi * GuideOffset, GuideOffset)), GuideRadius, GuideColor);
        base.Render(args);
    }

    private partial void OnPipeLayerPlacement(EntityUid gridId)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null || !_handsSystem.TryGetActiveItem(player.Value, out var held) ||
            !_entityManager.TryGetComponent<RCDComponent>(held, out var rcd) || !rcd.IsRpd)
            return;

        var offset = _unalignedMouseCoords.Position - MouseCoords.Position;
        var layer = AtmosPipeLayer.Primary;
        if (offset.Length() > PipeLayerDeadzone)
        {
            var gridRotation = _transformSystem.GetWorldRotation(gridId);
            var direction = (new Angle(offset) + _eyeManager.CurrentEye.Rotation + gridRotation + Math.PI / 2).GetCardinalDir();
            layer = direction is Direction.North or Direction.East
                ? AtmosPipeLayer.Secondary
                : AtmosPipeLayer.Tertiary;
        }

        _rcdSystem.SetConstructionPipeLayer((held.Value, rcd), layer);
    }
}
