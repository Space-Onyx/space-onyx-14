// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Ghost;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Ghost;

public sealed partial class GhostSystem
{
    public event Action<GhostWarpMenuResponseEvent>? GhostWarpMenuResponse;

    private void InitializeGhostWarpMenu()
    {
        SubscribeNetworkEvent<GhostWarpMenuResponseEvent>(OnGhostWarpMenuResponse);
    }

    private void OnGhostWarpMenuResponse(GhostWarpMenuResponseEvent message)
    {
        if (IsGhost)
            GhostWarpMenuResponse?.Invoke(message);
    }

    public void RequestGhostWarpMenu()
    {
        RaiseNetworkEvent(new GhostWarpMenuRequestEvent());
    }
}
