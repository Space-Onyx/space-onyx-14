using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Medical;

[Serializable, NetSerializable]
public readonly record struct HealthAnalyzerWoundDiagnostic(
    FractureGrade Fracture,
    FractureTreatment FractureTreatment,
    float BleedingRate,
    BleedingTreatment BleedingTreatment,
    ushort ScarCount)
{
    public bool HasFindings =>
        Fracture != FractureGrade.None || BleedingRate > 0f || ScarCount > 0;
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
