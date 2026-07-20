using Content.Shared._Onyx.Wounds;

namespace Content.Shared._Onyx.Targeting;

public sealed partial class PartStatusSystem : EntitySystem
{
    // ponytail: Body-part capacities are unavailable. Replace with profile-relative thresholds when exposed.
    public static PartDamageSeverity GetSeverity(float damage) => damage switch
    {
        <= 0f => PartDamageSeverity.None,
        < 15f => PartDamageSeverity.Minor,
        < 40f => PartDamageSeverity.Moderate,
        < 70f => PartDamageSeverity.Severe,
        _ => PartDamageSeverity.Critical,
    };

    public static PartStatus Missing => new(false, PartDamageSeverity.None, false, FractureGrade.None, false);
}
