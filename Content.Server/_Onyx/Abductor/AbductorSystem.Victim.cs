// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Onyx.Abductor;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared.Database;
using Robust.Shared.Player;
namespace Content.Server._Onyx.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private IAdminLogManager _adminLogManager = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;

    private static readonly string DefaultAbductorVictimRule = "AbductorVictim";
    public void InitializeVictim()
    {
        SubscribeLocalEvent<AbductorOrganComponent, SurgeryOrganInsertedEvent>(OnAbductorOrganInserted);
    }

    private void OnAbductorOrganInserted(Entity<AbductorOrganComponent> ent, ref SurgeryOrganInsertedEvent args)
    {
        if (!HasComp<AbductorComponent>(args.User)
            || HasComp<AbductorComponent>(args.Body)
            || !TryComp<AbductorVictimComponent>(args.Body, out var victimComp)
            || victimComp.Implanted
            || !_mind.TryGetMind(args.Body, out var mindId, out _)
            || !TryComp<ActorComponent>(args.Body, out var actor))
            return;

        if (!_role.MindHasRole<AbductorVictimRoleComponent>(mindId, out _))
        {
            _antag.ForceMakeAntag<AbductorVictimRuleComponent>(actor.PlayerSession, DefaultAbductorVictimRule, "AbductorVictim");
            victimComp.Implanted = true;

            _adminLogManager.Add(LogType.Mind,
                LogImpact.Medium,
                $"{ToPrettyString(args.User)} has given {ToPrettyString(args.Body)} an abductee objective.");

        }

    }
}
