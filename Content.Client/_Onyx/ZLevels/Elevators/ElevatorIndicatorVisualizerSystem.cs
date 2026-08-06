using System;
using Content.Shared._Onyx.ZLevels.Elevators;
using Content.Shared._Onyx.ZLevels.Elevators.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.ZLevels.Elevators;

public sealed partial class ElevatorIndicatorVisualizerSystem : VisualizerSystem<ElevatorIndicatorComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, ElevatorIndicatorComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<int>(uid, ElevatorIndicatorVisuals.Floor, out var floor, args.Component))
            return;

        var digit = Math.Abs(floor) % 10;
        _sprite.LayerSetRsiState((uid, args.Sprite), ElevatorVisualLayers.Digit, $"lift_indo-num{digit}");
    }
}
