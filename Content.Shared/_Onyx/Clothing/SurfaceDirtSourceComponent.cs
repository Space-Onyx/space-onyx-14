// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.FixedPoint;

namespace Content.Shared._Onyx.Clothing;

[RegisterComponent]
public sealed partial class SurfaceDirtSourceComponent : Component
{
    [DataField]
    public string Solution = "puddle";

    [DataField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(1);
}
