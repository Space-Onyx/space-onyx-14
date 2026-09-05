// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Onyx.Movement;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class JumpComponent : Component
{
    [DataField]
    public float Distance = 0.5f;

    [DataField]
    public float SprintDistance = 1.2f;

    [DataField]
    public float TableDistance = 1.2f;

    [DataField]
    public float Speed = 8f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.75);

    [DataField]
    public TimeSpan Windup = TimeSpan.FromSeconds(0.3);

    [DataField]
    public float StaminaCost = 30f;

    [DataField]
    public float MinimumStamina = 5f;

    [DataField]
    public float WeightlessStaminaCostMultiplier = 0.4f;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextJump;

    [AutoNetworkedField]
    public bool IsJumping;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan JumpStarted;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan JumpEnds;

    [AutoNetworkedField]
    public bool PendingJump;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan LaunchTime;

    [AutoNetworkedField]
    public Vector2 JumpDirection;

    [AutoNetworkedField]
    public float PendingDistance;

    [AutoNetworkedField]
    public bool PendingTableJump;
}
