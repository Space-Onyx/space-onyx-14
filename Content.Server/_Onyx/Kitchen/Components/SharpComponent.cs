// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Onyx.Kitchen.Components;

/// <summary>
/// Marks an entity as capable of handheld butchering.
/// </summary>
[RegisterComponent]
public sealed partial class SharpComponent : Component
{
    public readonly HashSet<EntityUid> Butchering = new();

    [DataField("butcherDelayModifier")]
    public float ButcherDelayModifier = 1f;
}
