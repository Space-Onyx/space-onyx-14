using Content.Shared.Body.Part;
using Content.Shared.Damage;

namespace Content.Shared.Armor;

public sealed partial class ArmorComponent
{
    /// <summary>
    /// Body part types protected by this armor. Empty protects every part.
    /// Inventory slot does not affect coverage.
    /// </summary>
    [DataField]
    public HashSet<BodyPartType> Coverage = [];

    /// <summary>
    /// Protected sides. Empty protects every symmetry.
    /// </summary>
    [DataField]
    public HashSet<BodyPartSymmetry> CoverageSymmetry = [];

    /// <summary>
    /// Ordered location-specific modifier overrides. The first matching entry is used.
    /// Falls back to the component's legacy coverage and modifiers when none match.
    /// </summary>
    [DataField]
    public List<ArmorPartModifier> PartModifiers = [];
}

[DataDefinition]
public sealed partial class ArmorPartModifier
{
    /// <summary>
    /// Matching body part types. Empty matches every type.
    /// </summary>
    [DataField]
    public HashSet<BodyPartType> Parts = [];

    /// <summary>
    /// Matching sides. Empty matches every symmetry.
    /// </summary>
    [DataField]
    public HashSet<BodyPartSymmetry> Symmetry = [];

    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;
}
