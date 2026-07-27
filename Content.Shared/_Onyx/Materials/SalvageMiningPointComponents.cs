using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Materials
{
    [RegisterComponent]
    public sealed partial class SalvageMiningPointProcessorComponent : Component;

    [RegisterComponent, NetworkedComponent]
    public sealed partial class SalvageMiningPointVendorComponent : Component;

}
