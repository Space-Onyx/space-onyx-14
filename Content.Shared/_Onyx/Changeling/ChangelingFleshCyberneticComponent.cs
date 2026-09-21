// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Changeling;

/// <summary>
/// Marks an organic organ disguised as a cybernetic organ.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ChangelingFleshCyberneticComponent : Component
{
    /// <summary>
    /// Cybernetic prototype whose appearance this organ copies.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId Prototype;
}
