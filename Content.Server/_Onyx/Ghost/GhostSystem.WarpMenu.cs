// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Onyx.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusIcon;
using Content.Shared.Warps;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Ghost;

public sealed partial class GhostSystem
{
    private static readonly ProtoId<AntagTagPrototype> OffStationAntagTag = "OffStation";

    [Dependency] private SharedRoleSystem _ghostWarpRoles = default!;

    private void InitializeGhostWarpMenu()
    {
        SubscribeNetworkEvent<GhostWarpMenuRequestEvent>(OnGhostWarpMenuRequest);
    }

    private void OnGhostWarpMenuRequest(GhostWarpMenuRequestEvent message, EntitySessionEventArgs args)
    {
        if (!CanGhostWarp(args.SenderSession, out var ghost))
        {
            Log.Warning($"User {args.SenderSession.Name} requested the ghost warp menu without being a ghost.");
            return;
        }

        var entries = GetGhostWarpMenuPlayers(ghost).Concat(GetGhostWarpMenuLocations()).ToList();
        RaiseNetworkEvent(new GhostWarpMenuResponseEvent(entries), args.SenderSession.Channel);
    }

    private IEnumerable<GhostWarpMenuEntry> GetGhostWarpMenuPlayers(EntityUid except)
    {
        foreach (var mindContainer in EntityQuery<MindContainerComponent>())
        {
            var entity = mindContainer.Owner;
            if (entity == except)
                continue;

            var mindId = mindContainer.Mind ?? mindContainer.LastMind;
            if (mindId == null || !TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
                continue;

            var roles = _ghostWarpRoles.MindGetAllRoleInfo((mindId.Value, mind));
            var antagRole = roles.FirstOrDefault(role => role.Antagonist);
            if (antagRole.Antagonist
                && ProtoMan.TryIndex<AntagPrototype>(antagRole.Prototype, out var antag)
                && antag.Tags.Contains(OffStationAntagTag)
                && !HasComp<GhostComponent>(entity))
            {
                yield return new GhostWarpMenuEntry(
                    GetNetEntity(entity),
                    Name(entity),
                    GhostWarpMenuCategory.Antagonist,
                    Loc.GetString(antag.Objective),
                    Loc.GetString(antag.Name));
                continue;
            }

            var category = GetGhostWarpMenuCategory(entity);
            if (category == null)
                continue;

            var jobName = Loc.GetString("generic-unknown-title");
            var department = "Specific";
            ProtoId<JobIconPrototype>? jobIcon = null;

            if (_jobs.MindTryGetJob(mindId, out var job))
            {
                jobName = job.LocalizedName;
                jobIcon = job.Icon;

                if (_jobs.TryGetDepartment(job.ID, out var departmentPrototype))
                    department = departmentPrototype.ID;
            }

            yield return new GhostWarpMenuEntry(
                GetNetEntity(entity),
                Name(entity),
                category.Value,
                jobName,
                department,
                jobIcon);
        }
    }

    private GhostWarpMenuCategory? GetGhostWarpMenuCategory(EntityUid entity)
    {
        if (HasComp<GhostComponent>(entity))
            return GhostWarpMenuCategory.Ghost;

        if (_mobState.IsDead(entity))
            return GhostWarpMenuCategory.Dead;

        if (TryComp<SSDIndicatorComponent>(entity, out var indicator) && indicator.IsSSD)
            return GhostWarpMenuCategory.Disconnected;

        if (_mobState.IsAlive(entity) || _mobState.IsCritical(entity))
            return GhostWarpMenuCategory.Alive;

        return null;
    }

    private IEnumerable<GhostWarpMenuEntry> GetGhostWarpMenuLocations()
    {
        var query = AllEntityQuery<WarpPointComponent>();
        while (query.MoveNext(out var uid, out var warp))
        {
            yield return new GhostWarpMenuEntry(
                GetNetEntity(uid),
                warp.Location == null ? Name(uid) : Loc.GetString(warp.Location),
                GhostWarpMenuCategory.Location,
                Description(uid));
        }
    }
}
