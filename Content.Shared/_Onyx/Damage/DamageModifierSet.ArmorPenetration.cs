// Content adapted from Goob Station (https://github.com/Goob-Station/Goob-Station), licensed under MIT.

using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Damage;

public partial class DamageModifierSet
{
    [DataField(customTypeSerializer: typeof(FlagSerializer<ArmorPierceFlags>))]
    public int IgnoreArmorPierceFlags;
}

public sealed class ArmorPierceFlags;

[Flags, Serializable]
[FlagsFor(typeof(ArmorPierceFlags))]
public enum PartialArmorPierceFlags
{
    None = 0,
    Positive = 1 << 0,
    Negative = 1 << 1,
    All = Positive | Negative,
}
