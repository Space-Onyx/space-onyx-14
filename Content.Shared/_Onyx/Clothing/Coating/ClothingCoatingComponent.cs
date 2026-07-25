using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Clothing.Coating;

[RegisterComponent, NetworkedComponent]
public sealed partial class ClothingCoatingComponent : Component
{
    [DataField(required: true)]
    public LocId CoatingName;

    [DataField(required: true)]
    public ComponentRegistry Components = new();
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CoatedClothingComponent : Component
{
    [DataField]
    public List<LocId> CoatingNames = [];
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SpeedModifierImmunityComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class VeryFlammableComponent : Component;
