using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Gambling.CoinFlipper;

[Serializable, NetSerializable]
public sealed partial class CoinFlipperDoAfterEvent : SimpleDoAfterEvent;