using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Weapons.Ranged;

[RegisterComponent, NetworkedComponent]
public sealed partial class PKAWeaponAttachmentsComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class GunUpgradeBayonetComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class GunUpgradeFlashlightComponent : Component;

[ByRefEvent]
public record struct GetRelayMeleeWeaponEvent(EntityUid? Found = null, bool Handled = false);

[Serializable, NetSerializable]
public enum PKAAttachmentVisuals : byte
{
    FlashlightEnabled,
}
