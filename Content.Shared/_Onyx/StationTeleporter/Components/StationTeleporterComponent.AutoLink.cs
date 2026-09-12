// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared.StationTeleporter.Components;

public sealed partial class StationTeleporterComponent
{
    /// <summary>
    /// Determines which teleporters are linked automatically on map init. Teleporters sharing the same
    /// <see cref="AutoLinkKey"/> and <see cref="AutoLinkPair"/> but having opposite <see cref="AutoLinkRole"/>
    /// are linked as a single pair. Used by the hotel network so an entrance on the station and its exit
    /// in the hotel find each other without manual setup.
    /// </summary>
    [DataField]
    public int? AutoLinkPair;

    /// <summary>
    /// Role of this teleporter inside its auto-linked pair. Together with <see cref="AutoLinkPair"/> it makes
    /// pairing deterministic: an <see cref="TeleporterAutoLinkRole.Entrance"/> only links to an
    /// <see cref="TeleporterAutoLinkRole.Exit"/> with the same pair index. <see cref="TeleporterAutoLinkRole.None"/>
    /// disables auto-linking for this teleporter.
    /// </summary>
    [DataField]
    public TeleporterAutoLinkRole AutoLinkRole = TeleporterAutoLinkRole.None;
}

/// <summary>
/// Role of a teleporter in an auto-linked pair.
/// </summary>
public enum TeleporterAutoLinkRole : byte
{
    /// <summary>
    /// Auto-linking is disabled.
    /// </summary>
    None,

    /// <summary>
    /// Placed on the station side. Links to the matching exit.
    /// </summary>
    Entrance,

    /// <summary>
    /// Placed on the destination side. Links to the matching entrance.
    /// </summary>
    Exit,
}
