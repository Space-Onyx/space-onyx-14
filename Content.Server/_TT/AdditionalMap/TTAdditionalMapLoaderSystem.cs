/*
 * License: MIT
 * Copyright: (c) 2025 TornadoTechnology
 */

using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Events;
using Content.Server.GameTicking.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._TT.AdditionalMap;

public sealed partial class TTAdditionalMapLoaderSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    // <Onyx-AdditionalMapInitialization-edited>
    [Dependency] private MapSystem _map = default!;

    private readonly List<MapId> _mapsToInitialize = new();
    // </Onyx-AdditionalMapInitialization-edited>

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LoadingMapsEvent>(OnGetMaps);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting); // <Onyx-AdditionalMapInitialization-edited>
    }

    private void OnGetMaps(LoadingMapsEvent args)
    {
        var firstMap = args.Maps[0];
        if (!_prototype.TryIndex<TTAdditionalMapPrototype>(firstMap.ID, out var proto))
            return;

        foreach (var mapProtoId in proto.MapProtoIds)
        {
            if (!_prototype.TryIndex(mapProtoId, out var mapProto))
                continue;

            // <Onyx-AdditionalMapInitialization-edited>
            _gameTicker.LoadGameMap(mapProto, out var mapId);
            _mapsToInitialize.Add(mapId);
            // </Onyx-AdditionalMapInitialization-edited>
        }
    }

    // <Onyx-AdditionalMapInitialization-edited>
    private void OnRoundStarting(RoundStartingEvent args)
    {
        foreach (var mapId in _mapsToInitialize)
        {
            if (_map.MapExists(mapId) && !_map.IsInitialized(mapId))
                _map.InitializeMap(mapId);
        }

        _mapsToInitialize.Clear();
    }
    // </Onyx-AdditionalMapInitialization-edited>
}
