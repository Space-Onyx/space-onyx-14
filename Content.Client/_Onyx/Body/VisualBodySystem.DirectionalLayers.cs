// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Onyx.Body;

namespace Content.Client.Body;

public sealed partial class VisualBodySystem
{
    [Dependency] private DirectionalLayeringSystem _directionalLayering = default!;
}
