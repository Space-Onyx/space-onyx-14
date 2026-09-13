// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Ghost;

/// <summary>
/// Spawns a ghost role using the player's selected character profile.
/// </summary>
[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class CharacterProfileGhostRoleSpawnerComponent : Component;

/// <summary>
/// Prevents a spawned ghost-role body from becoming available for takeover again.
/// </summary>
[RegisterComponent]
public sealed partial class OneShotGhostRoleBodyComponent : Component;
