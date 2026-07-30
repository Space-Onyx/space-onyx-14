// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Resin;

[RegisterComponent]
public sealed partial class AreaSpawnerComponent : Component
{
    [DataField] public int Radius = 3;
    [DataField(required: true)] public EntProtoId SpawnPrototype;
    [DataField] public TimeSpan SpawnDelay = TimeSpan.FromSeconds(3);
    [DataField] public float MinTime = 1f;
    [DataField] public float MaxTime = 5f;
    public TimeSpan SpawnAt;
    public readonly HashSet<EntityUid> Spawned = [];
}
