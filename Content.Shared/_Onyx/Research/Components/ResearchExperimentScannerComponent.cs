// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Audio;

namespace Content.Shared._Onyx.Research.Components;

[RegisterComponent]
public sealed partial class ResearchExperimentScannerComponent : Component
{
    [DataField]
    public float Range = 2f;

    [DataField]
    public SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");

    [DataField]
    public SoundSpecifier FailureSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    public string LastResult = string.Empty;
}

[RegisterComponent]
public sealed partial class ResearchExperimentMachineComponent : Component
{
    [DataField]
    public string ContainerId = "research-experiment-scanner";

    [DataField]
    public TimeSpan ScanDuration = TimeSpan.FromSeconds(3.5);

    [DataField]
    public TimeSpan AnimationDuration = TimeSpan.FromSeconds(1.15);

    [DataField]
    public SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");

    [DataField]
    public SoundSpecifier FailureSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    public bool Processing;
    public string LastSubject = string.Empty;
    public string LastResult = string.Empty;
}
