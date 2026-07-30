using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.CosmicCult.Components;

/// <summary>
/// Component for revealing cosmic cultists to the crew.
/// </summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class CosmicStarMarkComponent : Component
{
    [DataField]
    public SpriteSpecifier Sprite = new SpriteSpecifier.Rsi(new("/Textures/_Onyx/CosmicCult/Effects/cultrevealed.rsi"), "vfx");
}

[Serializable, NetSerializable]
public enum CosmicRevealedKey
{
    Key
}
