using System.Numerics;
using Content.Shared._Onyx.Detection;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleNavControl
{
    private DetectionSystem _detection = default!;

    private void InitializeDetection()
    {
        _detection = EntManager.System<DetectionSystem>();
    }

    private bool TryGetDetectionLevel(Entity<MapGridComponent> grid, out DetectionLevel level)
    {
        level = _consoleEntity is { } detector
            ? _detection.GetLevel(grid, detector)
            : DetectionLevel.Detected;
        return _consoleEntity == null || level != DetectionLevel.Undetected;
    }

    private string? GetDetectionLabel(Entity<MapGridComponent> grid, DetectionLevel level, string? label)
    {
        return level == DetectionLevel.Partial
            ? $"{_detection.GetLevelLabel(level)}: {_detection.GetMassLabel(grid)}"
            : label;
    }

    private void DrawDetectedGrid(
        DrawingHandleScreen handle,
        Matrix3x2 gridToView,
        Entity<MapGridComponent> grid,
        Color color,
        DetectionLevel level)
    {
        if (level != DetectionLevel.Detected)
            return;

        DrawGrid(handle, gridToView, grid, color);
        DrawDocks(handle, grid.Owner, gridToView);
    }
}
