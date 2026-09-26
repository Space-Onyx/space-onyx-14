// Content adapted from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/6972), licensed under AGPL-3.0-or-later.

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Projectiles;

/// <summary>
/// Sent to nearby clients when an entity auto-dodges so they play the dodge visuals.
/// </summary>
[Serializable, NetSerializable]
public sealed class AutoDodgeEffectEvent(NetEntity entity, Vector2 direction) : EntityEventArgs
{
    public NetEntity Entity = entity;

    public Vector2 Direction = direction;
}
