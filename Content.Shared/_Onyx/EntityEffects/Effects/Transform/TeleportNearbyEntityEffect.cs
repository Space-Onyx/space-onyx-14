using System.Numerics;
using Content.Shared.EntityEffects;

namespace Content.Shared._Onyx.EntityEffects.Effects.Transform;

public sealed partial class TeleportNearby : EntityEffectBase<TeleportNearby>
{
    [DataField]
    public float Range = 7f;

    [DataField]
    public Vector2 Radius = new(5f, 20f);

    [DataField]
    public int Attempts = 10;
}
