// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Materials;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130
namespace Content.Shared.Lathe;

public sealed partial class LatheComponent
{
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int>? ActiveMaterialCost;
}
