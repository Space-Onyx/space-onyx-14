using Robust.Shared.Serialization;
// <Onyx-HealthAnalyzer-StatusDoll>
using Content.Shared.Damage;
using Content.Shared._Onyx.Medical;
using Content.Shared._Onyx.Targeting;
using Content.Shared.FixedPoint;
using System.Collections.Generic;
// </Onyx-HealthAnalyzer-StatusDoll>

namespace Content.Shared.MedicalScanner;

/// <summary>
/// Contains the current state of a health analyzer control. Used for the health analyzer and cryo pod.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerUiState : BoundUserInterfaceState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool ScanMode;
    public bool Bleeding;
    public bool Unrevivable;
    // <Onyx-HealthAnalyzer-StatusDoll>
    public Dictionary<TargetBodyPart, DamageSpecifier>? PartDamage;
    public HealthAnalyzerWoundDiagnostics? WoundDiagnostics;
    public FixedPoint2? VitalDamage;
    public List<HealthAnalyzerOrganInfo>? Organs;
    public List<HealthAnalyzerChemicalInfo>? Chemicals;
    // </Onyx-HealthAnalyzer-StatusDoll>

    public HealthAnalyzerUiState() {}

    // <Onyx-HealthAnalyzer-StatusDoll-edited>
    public HealthAnalyzerUiState(
        NetEntity? targetEntity,
        float temperature,
        float bloodLevel,
        bool scanMode,
        bool bleeding,
        bool unrevivable,
        Dictionary<TargetBodyPart, DamageSpecifier>? partDamage = null,
        HealthAnalyzerWoundDiagnostics? woundDiagnostics = null,
        FixedPoint2? vitalDamage = null,
        List<HealthAnalyzerOrganInfo>? organs = null,
        List<HealthAnalyzerChemicalInfo>? chemicals = null)
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        PartDamage = partDamage;
        WoundDiagnostics = woundDiagnostics;
        VitalDamage = vitalDamage;
        Organs = organs;
        Chemicals = chemicals;
    }
    // </Onyx-HealthAnalyzer-StatusDoll-edited>
}
