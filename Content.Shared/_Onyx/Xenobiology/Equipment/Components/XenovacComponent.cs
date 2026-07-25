using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Xenobiology.Equipment.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenovacComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier? SuctionSound = new SoundPathSpecifier("/Audio/Effects/zzzt.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? ReleaseSound = new SoundPathSpecifier("/Audio/Effects/trashbag3.ogg");

    [DataField, AutoNetworkedField]
    public EntityWhitelist Whitelist = new();

    [ViewVariables, AutoNetworkedField]
    public EntityUid? LinkedTank;
}
