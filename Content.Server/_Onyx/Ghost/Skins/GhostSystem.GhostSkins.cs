// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Preferences.Managers;
using Content.Shared._Onyx.Ghost.Skins;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Ghost;

public sealed partial class GhostSystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IServerPreferencesManager _serverPrefs = default!;

    /// <summary>
    /// Applies the user's selected ghost skin to any freshly spawned observer,
    /// regular or admin. Returns false when the vanilla look should be kept.
    /// </summary>
    public bool TryApplyGhostSkin(EntityUid ghost, NetUserId? userId)
    {
        if (userId is null)
            return false;

        if (!_serverPrefs.TryGetCachedPreferences(userId.Value, out var prefs))
            return false;

        if (!_proto.TryIndex(prefs.GhostSkin, out var skin))
            return false;

        if (skin.Sprite is null || skin.State is null)
            return false;

        if (!_player.TryGetSessionById(userId.Value, out var session))
            return false;

        if (!skin.CanUse(session, out _, out _))
            return false;

        var comp = EnsureComp<GhostSkinComponent>(ghost);
        comp.Skin = skin.ID;
        Dirty(ghost, comp);
        return true;
    }
}
