// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.ActiveAction;

[Serializable, NetSerializable]
public sealed class SetActiveActionEvent(string text) : EntityEventArgs
{
    public readonly string Text = text;
}
