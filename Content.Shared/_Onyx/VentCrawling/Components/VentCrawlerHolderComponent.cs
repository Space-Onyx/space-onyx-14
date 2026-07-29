// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 ss14-Starlight <ss14-Starlight@outlook.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Content.Shared.Atmos.Components;

namespace Content.Shared._Onyx.VentCrawling;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VentCrawlerHolderComponent : Component
{
    private Container? _container;

    public Container Container
    {
        get => _container ?? throw new InvalidOperationException("Container not initialized");
        set => _container = value;
    }

    [ViewVariables] public float Progress { get; set; }
    public bool IsMoving;
    [ViewVariables] public EntityUid? PreviousTube { get; set; }
    [ViewVariables, AutoNetworkedField] public EntityUid? NextTube { get; set; }
    [ViewVariables] public Direction PreviousDirection { get; set; } = Direction.Invalid;
    [ViewVariables, AutoNetworkedField] public EntityUid? CurrentTube { get; set; }
    [ViewVariables] public bool FirstEntry { get; set; }
    [ViewVariables] public Direction CurrentDirection { get; set; } = Direction.Invalid;
    [ViewVariables] public Direction TravelDirection { get; set; } = Direction.Invalid;
    [ViewVariables] public bool DirectionQueued { get; set; }
    [ViewVariables] public bool IsExitingVentCraws { get; set; }
    [ViewVariables, AutoNetworkedField] public AtmosPipeLayer PipeLayer { get; set; }
    [ViewVariables, AutoNetworkedField] public uint LayerSelectionSequence { get; set; }

    public EntityUid? CrawlSoundEntity;

    [DataField("crawlSound")]
    public SoundCollectionSpecifier CrawlSound { get; set; } = new("VentCrawlingSounds", AudioParams.Default.WithVolume(-5f));

    [DataField]
    public float TilesPerSecond = 4f;
}

[ByRefEvent]
public record struct VentCrawlingExitEvent
{
    public TransformComponent? HolderTransform;
}

[ByRefEvent]
public record struct VentCrawlerLayerSelectedEvent(int Step, byte Layers);
