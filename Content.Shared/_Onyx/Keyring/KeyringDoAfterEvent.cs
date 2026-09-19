// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Keyring;

[Serializable, NetSerializable]
public sealed partial class KeyringDoAfterEvent : SimpleDoAfterEvent;
