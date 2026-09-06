// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Research.Components;

[Serializable, NetSerializable]
public enum ResearchExperimentScannerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ResearchExperimentMachineUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class OpenExperimentServerMenuMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RunResearchExperimentMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ResearchExperimentUiEntry(
    string name,
    string description,
    List<ResearchExperimentTaskUiEntry> tasks,
    ResearchExperimentUiStatus status)
{
    public string Name = name;
    public string Description = description;
    public List<ResearchExperimentTaskUiEntry> Tasks = tasks;
    public ResearchExperimentUiStatus Status = status;
}

[Serializable, NetSerializable]
public enum ResearchExperimentUiStatus : byte
{
    Active,
    Locked,
    Completed,
}

[Serializable, NetSerializable]
public sealed class ResearchExperimentTaskUiEntry(string goal, int progress, int target)
{
    public string Goal = goal;
    public int Progress = progress;
    public int Target = target;
}

[Serializable, NetSerializable]
public sealed class ResearchExperimentScannerState(
    string? serverName,
    List<ResearchExperimentUiEntry> experiments,
    string lastResult) : BoundUserInterfaceState
{
    public string? ServerName = serverName;
    public List<ResearchExperimentUiEntry> Experiments = experiments;
    public string LastResult = lastResult;
}

[Serializable, NetSerializable]
public sealed class ResearchExperimentMachineBuiState(
    string? serverName,
    List<ResearchPointAmount> balances,
    List<ResearchExperimentUiEntry> experiments,
    string status,
    string lastResult) : BoundUserInterfaceState
{
    public string? ServerName = serverName;
    public List<ResearchPointAmount> Balances = balances;
    public List<ResearchExperimentUiEntry> Experiments = experiments;
    public string Status = status;
    public string LastResult = lastResult;
}
