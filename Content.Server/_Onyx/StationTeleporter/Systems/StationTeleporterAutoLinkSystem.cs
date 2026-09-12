// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.StationTeleporter.Systems;
using Content.Shared.StationTeleporter.Components;

namespace Content.Server._Onyx.StationTeleporter.Systems;

/// <summary>
/// Automatically links teleporters that opt into auto-linking on map init. Kept separate from
/// <see cref="StationTeleporterSystem"/> so the hotel network logic stays isolated from the vanilla chip system.
/// </summary>
public sealed partial class StationTeleporterAutoLinkSystem : EntitySystem
{
    [Dependency] private StationTeleporterSystem _stationTeleporter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationTeleporterComponent, MapInitEvent>(OnTeleporterMapInit);
    }

    private void OnTeleporterMapInit(Entity<StationTeleporterComponent> ent, ref MapInitEvent args)
    {
        _stationTeleporter.TryAutoLink(ent);
    }
}
