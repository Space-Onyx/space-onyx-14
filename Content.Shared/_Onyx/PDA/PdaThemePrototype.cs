// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.PDA;

[Prototype]
public sealed partial class PdaThemePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public Color Color = Color.White;
}
