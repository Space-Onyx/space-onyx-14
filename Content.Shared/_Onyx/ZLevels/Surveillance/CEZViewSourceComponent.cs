using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.ZLevels.Surveillance;

/// <summary>
/// Marks a remote eye/camera that should receive z-level PVS coverage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEZViewSourceComponent : Component;
