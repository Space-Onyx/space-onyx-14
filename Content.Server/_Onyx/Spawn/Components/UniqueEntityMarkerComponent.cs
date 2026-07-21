// SPDX-FileCopyrightText: 2024 BombasterDS <115770678+BombasterDS@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Onyx.Spawn.Components;

/// <summary>
///     Marker component for a unique entity.
/// </summary>
[RegisterComponent]
public sealed partial class UniqueEntityMarkerComponent : Component
{
    /// <summary>
    ///     Marker name used by the uniqueness check.
    /// </summary>
    [DataField]
    public string? MarkerName;

    /// <summary>
    ///     If true, only markers on a station are considered. Otherwise, markers work globally.
    /// </summary>
    [DataField]
    public bool StationOnly = true;
}
