// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Ghost;

[Serializable, NetSerializable]
public sealed class GhostReturnToLobbyEuiState(bool canConfirm) : EuiStateBase
{
    public bool CanConfirm { get; } = canConfirm;
}

[Serializable, NetSerializable]
public sealed class GhostReturnToLobbyConfirmMessage : EuiMessageBase;
