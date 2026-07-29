using Content.Shared._Onyx.Salvage.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Salvage.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(MiningVoucherSystem))]
public sealed partial class MiningVoucherComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist VendorWhitelist;

    [DataField]
    public SoundSpecifier? RedeemSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
}
