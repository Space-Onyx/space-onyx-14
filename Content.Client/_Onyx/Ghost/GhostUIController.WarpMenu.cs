// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Ghost;
using Content.Shared._Onyx.Ghost;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.UserInterface.Systems.Ghost;

public sealed partial class GhostUIController
{
    private void InitializeGhostWarpMenu(GhostSystem system)
    {
        system.GhostWarpMenuResponse += OnGhostWarpMenuResponse;
    }

    private void ShutdownGhostWarpMenu(GhostSystem system)
    {
        system.GhostWarpMenuResponse -= OnGhostWarpMenuResponse;
    }

    private void OnGhostWarpMenuResponse(GhostWarpMenuResponseEvent message)
    {
        Gui?.TargetWindow.UpdateWarpMenu(message.Entries);
    }

    private void RequestGhostWarpMenu()
    {
        Gui?.TargetWindow.PrepareGhostWarpMenu();
        _system?.RequestGhostWarpMenu();
    }
}
