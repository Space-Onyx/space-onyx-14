// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Research.Components;

public sealed partial class TechnologyDatabaseComponent
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<ResearchExperimentPrototype>> UnlockedExperiments = [];

    [DataField, AutoNetworkedField]
    public List<ProtoId<ResearchExperimentPrototype>> ActiveExperiments = [];

    [DataField, AutoNetworkedField]
    public List<ProtoId<ResearchExperimentPrototype>> CompletedExperiments = [];

    [DataField, AutoNetworkedField]
    public List<ResearchExperimentProgress> ExperimentProgress = [];
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct ResearchExperimentProgress
{
    [DataField]
    public ProtoId<ResearchExperimentPrototype> Experiment;

    [DataField]
    public List<ResearchExperimentTaskProgress> Tasks = [];
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct ResearchExperimentTaskProgress
{
    [DataField]
    public int Progress;

    [DataField]
    public int Target;

    [DataField]
    public HashSet<string> ScannedPrototypes = [];

    [DataField]
    public HashSet<NetEntity> ScannedEntities = [];
}
