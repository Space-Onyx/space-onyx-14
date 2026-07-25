using Content.Shared._Onyx.Mobs.Growth;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Mobs.Growth;

public sealed partial class MobGrowthVisualizerSystem : VisualizerSystem<MobGrowthComponent>
{
    protected override void OnAppearanceChange(EntityUid uid,
        MobGrowthComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<string>(uid, MobGrowthVisuals.Stage, out var stageId, args.Component) ||
            !component.Stages.TryGetValue(stageId, out var stage) ||
            stage.Sprite is not { } rsi)
        {
            return;
        }

        SpriteSystem.LayerSetRsi((uid, args.Sprite), stage.Layer, rsi, stage.State);
    }
}
