using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Targeting;

[Serializable, NetSerializable]
public enum DamageDistribution : byte
{
    SplitEvenly,
    SplitByPartWeight,
    SplitWithVariation,
}
