using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Salvage.Bosses.Megafauna.Components;

/// <summary>
/// Makes megafauna immune to damage without an origin and any damage while AI is inactive.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MegafaunaGodmodeComponent : Component;
