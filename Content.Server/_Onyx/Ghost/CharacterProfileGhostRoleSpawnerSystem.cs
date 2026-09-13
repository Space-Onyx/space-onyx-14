// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server._Onyx.Economy;
using Content.Server.Station.Systems;
using Content.Shared._Onyx.Ghost;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;

namespace Content.Server._Onyx.Ghost;

public sealed partial class CharacterProfileGhostRoleSpawnerSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private BankCardSystem _bankCard = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CharacterProfileGhostRoleSpawnerComponent, TakeGhostRoleEvent>(OnTakeRole);
        SubscribeLocalEvent<OneShotGhostRoleBodyComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private void OnTakeRole(Entity<CharacterProfileGhostRoleSpawnerComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (args.TookRole ||
            !TryComp<GhostRoleComponent>(ent, out var ghostRole) ||
            ghostRole.Taken ||
            MetaData(ent).EntityPaused)
            return;

        var profile = _gameTicker.GetPlayerProfile(args.Player);
        var mob = _stationSpawning.SpawnPlayerMob(Transform(ent).Coordinates, ghostRole.JobProto, profile, null);
        EnsureComp<OneShotGhostRoleBodyComponent>(mob);

        var spawnedEvent = new GhostRoleSpawnerUsedEvent(ent, mob);
        RaiseLocalEvent(mob, ref spawnedEvent);
        EnsureComp<MindContainerComponent>(mob);
        _ghostRole.GhostRoleInternalCreateMindAndTransfer(args.Player, ent, mob, ghostRole);
        if (ghostRole.JobProto is { } job && _mind.TryGetMind(mob, out var mindId, out var mind))
        {
            _roles.MindAddJobRole(mindId, mind, jobPrototype: job);
            _bankCard.SetupPlayerAccount(mob, job.Id);
        }

        args.TookRole = true;
        QueueDel(ent);
    }

    private void OnMindRemoved(Entity<OneShotGhostRoleBodyComponent> ent, ref MindRemovedMessage args)
    {
        RemCompDeferred<GhostTakeoverAvailableComponent>(ent);
        RemCompDeferred<GhostRoleComponent>(ent);
        RemCompDeferred<ToggleableGhostRoleComponent>(ent);
    }

}
