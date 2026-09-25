// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Prototypes;

public sealed partial class TechnologyPrototype
{
    /// <summary>
    /// Optional manual eighth-cell waypoints keyed by prerequisite technology.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<TechnologyPrototype>, List<Vector2i>> ConnectionRoutes { get; private set; } = new();

    [DataField]
    public Dictionary<ProtoId<TechnologyPrototype>, ResearchConnectionVisuals> ConnectionVisuals { get; private set; } = new();
}

[DataDefinition]
public sealed partial class ResearchConnectionVisuals
{
    [DataField]
    public ResearchConnectionThumbnailMode Thumbnail = ResearchConnectionThumbnailMode.Auto;

    [DataField]
    public ResearchConnectionBridgeMode Bridge = ResearchConnectionBridgeMode.Auto;

    [DataField]
    public Vector2i? SourceThumbnailPosition;

    [DataField]
    public Vector2i? TargetThumbnailPosition;

    [DataField]
    public List<Vector2i> SourceThumbnailRoute = new();

    [DataField]
    public List<Vector2i> TargetThumbnailRoute = new();

    [DataField]
    public Vector2? SourcePort;

    [DataField]
    public Vector2? TargetPort;
}

public enum ResearchConnectionThumbnailMode
{
    Auto,
    Always,
    Never,
}

public enum ResearchConnectionBridgeMode
{
    Auto,
    Over,
    Under,
    None,
}
