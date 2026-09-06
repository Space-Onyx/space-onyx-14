// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Shared.Eui;
using Content.Shared._Onyx.Ghost;
using JetBrains.Annotations;

namespace Content.Server._Onyx.Ghost;

[UsedImplicitly]
public sealed class GhostReturnToLobbyEui(GhostSystem ghost, bool canConfirm) : BaseEui
{
    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new GhostReturnToLobbyEuiState(canConfirm);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not GhostReturnToLobbyConfirmMessage)
            return;

        ghost.TryReturnToLobby(Player);
        Close();
    }
}
