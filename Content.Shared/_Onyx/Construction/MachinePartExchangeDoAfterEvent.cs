// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Construction;

[Serializable, NetSerializable]
public sealed partial class MachinePartExchangeDoAfterEvent : SimpleDoAfterEvent;
