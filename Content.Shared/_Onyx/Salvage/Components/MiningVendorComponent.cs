using Content.Shared._Onyx.Salvage.Systems;
using Content.Shared.Thief;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(MiningVoucherSystem))]
public sealed partial class MiningVendorComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<ThiefBackpackSetPrototype>> Kits = [];
}
