// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Research;

[Serializable, NetSerializable]
public enum ResearchExperimentMachineVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum ResearchExperimentMachineState : byte
{
    Idle,
    Closing,
    Scanning,
    Opening,
}

[Serializable, NetSerializable]
public enum ResearchExperimentMachineLayers : byte
{
    Base,
}
