using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.CosmicCult.Components;

/// <summary>
/// Component for revealing cosmic cultists to the crew.
/// </summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class RogueAscendedInfectionComponent : Component
{
    [DataField]
    public SpriteSpecifier Sprite = new SpriteSpecifier.Rsi(new("/Textures/_Onyx/CosmicCult/Effects/ascendantinfection.rsi"), "vfx");

    [DataField]
    public bool HadMoods;
}

[Serializable, NetSerializable]
public enum AscendedInfectionKey
{
    Key
}
