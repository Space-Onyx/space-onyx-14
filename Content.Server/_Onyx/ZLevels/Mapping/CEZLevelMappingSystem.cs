/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Onyx.ZLevels.Core;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.ZLevels.Mapping;

public sealed partial class CEZLevelMappingSystem : EntitySystem
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEZLevelMapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEZLevelMapComponent, CEMapAddedIntoZNetworkEvent>(OnAddedIntoZNetwork);
    }

    private void OnAddedIntoZNetwork(Entity<CEZLevelMapComponent> ent, ref CEMapAddedIntoZNetworkEvent args)
    {
        if (_map.IsInitialized(ent))
        {
            EntityManager.AddComponents(ent, args.Network.Comp.Components);
            return;
        }

        foreach (var existingMapUid in args.Network.Comp.ZLevels.Values)
        {
            if (existingMapUid.HasValue && _map.IsInitialized(existingMapUid.Value))
            {
                _map.InitializeMap(ent.Owner);
                return;
            }
        }
    }

    private void OnMapInit(Entity<CEZLevelMapComponent> ent, ref MapInitEvent args)
    {
        if (_zLevels.TryZNetwork((ent, ent.Comp), out var network))
            EntityManager.AddComponents(ent, network.Value.Comp.Components);
    }
}
