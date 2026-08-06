/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Onyx.ZLevels.Core.EntitySystems;
using Robust.Server.GameObjects;

namespace Content.Server._Onyx.ZLevels.Core;

public sealed partial class CEZLevelsSystem : CESharedZLevelsSystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    private bool _serverInitialized;

    public override void Initialize()
    {
        if (_serverInitialized)
            return;

        base.Initialize();
        _serverInitialized = true;
        InitView();
        InitGridSync();
        InitItems();
        InitTransitionBudget();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateView(frameTime);
        UpdateGridSync(frameTime);
        UpdateItems(frameTime);
    }
}