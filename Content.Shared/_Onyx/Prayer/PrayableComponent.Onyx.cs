// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.Audio;

namespace Content.Shared.Prayer;

public sealed partial class PrayableComponent
{
    /// <summary>
    /// Optional sound played to admins with the prayer notification.
    /// </summary>
    [DataField]
    public SoundSpecifier? NotificationSound;
}
