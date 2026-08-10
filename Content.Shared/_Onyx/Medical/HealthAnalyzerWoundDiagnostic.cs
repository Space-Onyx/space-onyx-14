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
    FixedPoint2 Pain)
{
    public bool HasFindings =>
        Fracture != FractureGrade.None || BleedingRate > 0f || ScarCount > 0 || Pain > FixedPoint2.Zero;
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
