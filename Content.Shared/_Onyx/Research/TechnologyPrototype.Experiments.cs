// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Research.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Prototypes;

public sealed partial class TechnologyPrototype
{
    [DataField]
    public List<ProtoId<ResearchExperimentPrototype>> RequiredExperiments = [];

    [DataField]
    public Dictionary<ProtoId<ResearchExperimentPrototype>, int> ExperimentDiscounts = [];

    [DataField]
    public List<ProtoId<ResearchExperimentPrototype>> UnlockedExperiments = [];
}
