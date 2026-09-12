// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.StationTeleporter.Components;

namespace Content.Shared.StationTeleporter;

public abstract partial class SharedStationTeleporterSystem
{
    /// <summary>
    /// Links the given teleporter with its matching auto-link partner, if it has one. Teleporters are paired
    /// deterministically by <see cref="StationTeleporterComponent.AutoLinkKey"/> and
    /// <see cref="StationTeleporterComponent.AutoLinkPair"/>, and must have opposite
    /// <see cref="StationTeleporterComponent.AutoLinkRole"/> values. An already linked teleporter is left alone,
    /// so a connection never holds more than two teleporters.
    /// </summary>
    /// <returns>True if a new link was established.</returns>
    public bool TryAutoLink(Entity<StationTeleporterComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.AutoLinkKey is null || comp.AutoLinkPair is null || comp.AutoLinkRole == TeleporterAutoLinkRole.None)
            return false;

        if (_link.GetLink(ent.Owner, out _))
            return false;

        var partnerRole = OppositeAutoLinkRole(comp.AutoLinkRole);

        // Find the partner first and leave the query before touching components: linking moves entities
        // between archetypes, which would invalidate an in-progress enumeration and component references.
        EntityUid? partner = null;
        var query = EntityQueryEnumerator<StationTeleporterComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == ent.Owner
                || other.AutoLinkKey != comp.AutoLinkKey
                || other.AutoLinkPair != comp.AutoLinkPair
                || other.AutoLinkRole != partnerRole
                || _link.GetLink(uid, out _))
                continue;

            partner = uid;
            break;
        }

        if (partner is null)
            return false;

        if (!_link.TryLink(ent.Owner, partner.Value))
            return false;

        // Remember the link on both ends so it is restored after a power cycle.
        if (TryComp<StationTeleporterComponent>(ent.Owner, out var selfComp))
            selfComp.LastLink = partner.Value;

        if (TryComp<StationTeleporterComponent>(partner.Value, out var partnerComp))
            partnerComp.LastLink = ent.Owner;

        return true;
    }

    private static TeleporterAutoLinkRole OppositeAutoLinkRole(TeleporterAutoLinkRole role)
    {
        return role switch
        {
            TeleporterAutoLinkRole.Entrance => TeleporterAutoLinkRole.Exit,
            TeleporterAutoLinkRole.Exit => TeleporterAutoLinkRole.Entrance,
            _ => TeleporterAutoLinkRole.None,
        };
    }
}
