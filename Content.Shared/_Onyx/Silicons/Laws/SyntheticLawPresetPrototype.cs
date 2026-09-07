// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Prototypes;
using Content.Shared.Silicons.Laws;
using Content.Shared.Preferences.Loadouts;

namespace Content.Shared._Onyx.Silicons.Laws;

[Prototype]
public sealed partial class SyntheticLawPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ProtoId<SiliconLawsetPrototype> Lawset;

    [DataField(required: true)]
    public List<ProtoId<RoleLoadoutPrototype>> Roles = new();
}
