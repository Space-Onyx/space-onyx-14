// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared._Onyx.Ghost;
using JetBrains.Annotations;

namespace Content.Client._Onyx.Ghost;

[UsedImplicitly]
public sealed class GhostReturnToLobbyEui : BaseEui
{
    private readonly GhostReturnToLobbyWindow _window = new();

    public GhostReturnToLobbyEui()
    {
        _window.OnConfirm += () => SendMessage(new GhostReturnToLobbyConfirmMessage());
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is GhostReturnToLobbyEuiState ghostState)
            _window.SetCanConfirm(ghostState.CanConfirm);
    }
}
