using Content.Shared.Hands.Components;
using Robust.Shared.Network;

namespace Content.Shared.Inventory;

public partial class InventorySystem
{
    [Dependency] private INetManager _netManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeEquip();
        InitializeRelay();
        InitializeSlots();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ShutdownSlots();
    }
}
