using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Execution;

/// <summary>
/// Used in any guns that shouldn't be able to be used for executions.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunExecutionBlacklistComponent : Component;