using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Medical;

[Serializable, NetSerializable]
public readonly record struct HealthAnalyzerWoundDiagnostic(
    FractureGrade Fracture,
    FractureTreatment FractureTreatment,
    float BleedingRate,
    BleedingTreatment BleedingTreatment,
    ushort ScarCount,
    FixedPoint2 Pain,
    List<HealthAnalyzerVisibleWound> VisibleWounds,
    BodyPartFunctionalityState Functionality,
    float InternalBleedingRate,
    HealthAnalyzerClottingPhase ClottingPhase)
{
    public bool HasFindings =>
        Fracture != FractureGrade.None || BleedingRate > 0f || ScarCount > 0 || Pain > FixedPoint2.Zero ||
        VisibleWounds.Count > 0 || Functionality != BodyPartFunctionalityState.Functional || InternalBleedingRate > 0f;
}

[Serializable, NetSerializable]
public readonly record struct HealthAnalyzerVisibleWound(LocId Name, LocId? StageName, int Count);

[Serializable, NetSerializable]
public enum HealthAnalyzerClottingPhase : byte
{
    NotApplicable,
    None,
    InProgress,
    Complete,
    Mixed,
}

[Serializable, NetSerializable]
public sealed class HealthAnalyzerWoundDiagnostics
{
    public readonly Dictionary<TargetBodyPart, HealthAnalyzerWoundDiagnostic> Parts;

    public HealthAnalyzerWoundDiagnostics(Dictionary<TargetBodyPart, HealthAnalyzerWoundDiagnostic> parts)
    {
        Parts = parts;
    }
}
