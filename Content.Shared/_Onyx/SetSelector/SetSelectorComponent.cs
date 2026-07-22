// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.SetSelector;

[RegisterComponent, Access(typeof(SetSelectorSystem))]
public sealed partial class SetSelectorComponent : Component
{
    [DataField]
    public List<ProtoId<SelectableSetPrototype>> PossibleSets = new();

    [DataField]
    public List<ProtoId<SelectableSetPrototype>> AvailableSets = new();

    [DataField]
    public List<int> SelectedSets = new();

    [DataField]
    public int MaxSelectedSets = 1;

    [DataField]
    public int SetsToSelect = -1;

    [DataField]
    public EntProtoId? SpawnedStoragePrototype;

    [DataField]
    public string? SpawnedStorageContainer;

    [DataField]
    public bool OpenSpawnedStorage;

    [DataField]
    public SoundSpecifier? ApproveSound;
}
