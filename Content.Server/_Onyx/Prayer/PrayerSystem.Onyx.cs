// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Server.Administration.Managers;
using Content.Shared.Prayer;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Prayer;

public sealed partial class PrayerSystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private void PlayAdminNotificationSound(PrayableComponent comp)
    {
        if (comp.NotificationSound == null)
            return;

        foreach (var admin in _admin.ActiveAdmins)
        {
            if (admin.AttachedEntity is not { } attached)
                continue;

            _audio.PlayPvs(comp.NotificationSound, attached);
        }
    }
}
