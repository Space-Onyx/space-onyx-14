// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Onyx.Movement;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class JumpComponent : Component
{
    [DataField]
    public float Distance = 0.5f;

    [DataField]
    public float SprintDistance = 0.9f;

    [DataField]
    public float TableDistance = 1.0f;

    [DataField]
    public float Speed = 8f;

    [DataField]
    public TimeSpan StationaryDuration = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public float StaminaCost = 30f;

    [DataField]
    public float MinimumStamina = 10f;

    [DataField]
    public float WeightlessStaminaCostMultiplier = 0.5f;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextJump;

    [AutoNetworkedField]
    public bool IsJumping;

    [AutoNetworkedField]
    public bool MountTable;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan JumpStarted;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan JumpEnds;
}
