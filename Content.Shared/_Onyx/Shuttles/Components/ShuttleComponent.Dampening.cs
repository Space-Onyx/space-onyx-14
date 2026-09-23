// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Shuttles.Events;

namespace Content.Shared.Shuttles.Components;

public sealed partial class ShuttleComponent
{
    [DataField]
    public InertiaDampeningMode DampeningMode = InertiaDampeningMode.Dampen;
}
