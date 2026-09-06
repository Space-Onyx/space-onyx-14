// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Prototypes;

#pragma warning disable IDE0130
namespace Content.Shared.Parallax.Biomes.Layers;

public sealed partial class BiomeEntityLayer
{
    [DataField]
    public Dictionary<EntProtoId, float> EntityWeights { get; private set; } = new();
}
