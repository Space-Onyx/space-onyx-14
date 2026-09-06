// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Research.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Research.Prototypes;

[Prototype]
public sealed partial class ResearchExperimentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId Description;

    [DataField]
    public bool Hidden;

    [DataField]
    public bool StartingExperiment;

    [DataField]
    public List<ProtoId<TechnologyPrototype>> RequiredTechnologies = [];

    [DataField]
    public List<ProtoId<ResearchExperimentPrototype>> RequiredExperiments = [];

    [DataField]
    public ExperimentSource SupportedSources = ExperimentSource.AnyScanner;

    [DataField(required: true)]
    public List<ResearchExperimentTask> Tasks = [];

    [DataField]
    public ResearchExperimentReward Reward = new();
}

[Flags]
public enum ExperimentSource : byte
{
    None = 0,
    ResearchConsole = 1 << 0,
    MachineScanner = 1 << 1,
    HandheldScanner = 1 << 2,
    AnyScanner = ResearchConsole | MachineScanner | HandheldScanner,
}

[DataDefinition]
public sealed partial class ResearchExperimentTask
{
    [DataField(required: true)]
    public LocId Goal;

    [DataField]
    public int Target = 1;

    [DataField]
    public bool RequireDifferentPrototypes;

    [DataField]
    public bool AllowRepeatedEntities;

    [DataField(required: true)]
    public List<ResearchExperimentRequirement> AnyOf = [];
}

[DataDefinition]
public sealed partial class ResearchExperimentRequirement
{
    [DataField]
    public List<EntProtoId> Prototypes = [];

    [DataField]
    public List<ProtoId<TagPrototype>> Tags = [];

    [DataField]
    public List<string> Components = [];

    [DataField]
    public List<ResearchExperimentCondition> Conditions = [];

    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    [DataField]
    public float? MinimumReagentPurity;

    [DataField]
    public string? Gas;

    [DataField]
    public float? MinimumGasPurity;

    [DataField]
    public float? MinimumExplosiveIntensity;
}

[Serializable, NetSerializable]
public enum ResearchExperimentCondition : byte
{
    Fish,
    RareFish,
    Cyborg,
    NonHumanHumanoid,
    Damaged,
}

[DataDefinition]
public sealed partial class ResearchExperimentReward
{
    [DataField]
    public List<ResearchPointAmount> Points = [];

    [DataField]
    public List<ProtoId<ResearchExperimentPrototype>> UnlockExperiments = [];

    [DataField]
    public List<ProtoId<TechnologyPrototype>> RevealTechnologies = [];
}

[Serializable, NetSerializable]
public enum ResearchExperimentAttemptResult : byte
{
    Progressed,
    NoCompatibleExperiment,
    NoMatch,
    AlreadyScanned,
}
