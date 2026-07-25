using Robust.Shared.Containers;

namespace Content.Shared.Clothing.Components;

public sealed partial class AttachedClothingComponent
{
    public const string ReplacedClothingContainerId = "replaced-clothing";

    [DataField]
    public string ReplacedClothingContainerIdField = ReplacedClothingContainerId;

    [ViewVariables]
    public ContainerSlot? ReplacedClothing;
}
