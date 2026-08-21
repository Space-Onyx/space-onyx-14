using System.Linq;
using Content.Shared._Onyx.Wounds;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Damage;

public sealed partial class HealthChange
{
    [DataField]
    public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];
}

public sealed partial class EvenHealthChange
{
    [DataField]
    public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];
}

public sealed partial class DistributedHealthChange
{
    [DataField]
    public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];
}

public sealed partial class MendFractures : EntityEffectBase<MendFractures>
{
    /// <summary>Fracture wound prototypes to treat. Empty means all fracture wounds.</summary>
    [DataField]
    public HashSet<ProtoId<WoundPrototype>> Wounds = ["BoneFractureWound"];

    [DataField]
    public FractureGrade MinimumGrade = FractureGrade.Hairline;

    [DataField]
    public FractureGrade MaximumGrade = FractureGrade.Comminuted;

    [DataField]
    public FixedPoint2 Amount = 1;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var wounds = Wounds.Count == 0
            ? Loc.GetString("entity-effect-guidebook-all-fractures")
            : string.Join(", ", Wounds.Select(id =>
                prototype.TryIndex(id, out WoundPrototype? wound) ? Loc.GetString(wound.Name) : id.Id));
        return Loc.GetString("entity-effect-guidebook-mend-fractures",
            ("chance", Probability),
            ("amount", Amount.Float()),
            ("wounds", wounds),
            ("minimumGrade", Loc.GetString($"fracture-grade-{MinimumGrade.ToString().ToLowerInvariant()}")),
            ("maximumGrade", Loc.GetString($"fracture-grade-{MaximumGrade.ToString().ToLowerInvariant()}")));
    }
}
