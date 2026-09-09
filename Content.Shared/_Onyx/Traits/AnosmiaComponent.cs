// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Traits;

[RegisterComponent]
public sealed partial class AnosmiaComponent : Component;

[ByRefEvent]
public record struct SmellAttemptEvent(bool Cancelled = false);
