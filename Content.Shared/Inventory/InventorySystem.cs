using Content.Shared.Hands.Components;
using Robust.Shared.Network;

namespace Content.Shared.Inventory;

public partial class InventorySystem
{
    [Dependency] private INetManager _netManager = default!;

    // <Onyx-BodyInventorySlots>
    private readonly Dictionary<EntityUid, SlotFlags> _pendingBodySlots = new();
    private readonly Dictionary<EntityUid, SlotFlags> _readyBodySlots = new();
    // </Onyx-BodyInventorySlots>

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

    // <Onyx-BodyInventorySlots>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_netManager.IsClient)
            return;

        foreach (var (uid, available) in _readyBodySlots)
        {
            if (!_pendingBodySlots.ContainsKey(uid))
                ApplyBodySlots(uid, available);
        }

        _readyBodySlots.Clear();
        foreach (var (uid, available) in _pendingBodySlots)
            _readyBodySlots[uid] = available;
        _pendingBodySlots.Clear();
    }
    // </Onyx-BodyInventorySlots>
}
