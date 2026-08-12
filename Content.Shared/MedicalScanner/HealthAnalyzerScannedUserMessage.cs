using Robust.Shared.Serialization;
// <Onyx-HealthAnalyzer-StatusDoll>
using Content.Shared.Damage;
using Content.Shared._Onyx.Medical;
using Content.Shared._Onyx.Targeting;
using System.Collections.Generic;
// </Onyx-HealthAnalyzer-StatusDoll>

namespace Content.Shared.MedicalScanner;

/// <summary>
/// On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public HealthAnalyzerUiState State;

    public HealthAnalyzerScannedUserMessage(HealthAnalyzerUiState state)
    {
        State = state;
    }
}

/// <summary>
/// Contains the current state of a health analyzer control. Used for the health analyzer and cryo pod.
/// </summary>
[Serializable, NetSerializable]
public struct HealthAnalyzerUiState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    // <Onyx-HealthAnalyzer-StatusDoll>
    public Dictionary<TargetBodyPart, DamageSpecifier>? PartDamage;
    public HealthAnalyzerWoundDiagnostics? WoundDiagnostics;
    // <Onyx-HealthAnalyzerOrgans-edited>
    public List<HealthAnalyzerOrganInfo>? Organs;
    // </Onyx-HealthAnalyzerOrgans-edited>
    public List<HealthAnalyzerChemicalInfo>? Chemicals; // <Onyx-HealthAnalyzerChemicals>
    // </Onyx-HealthAnalyzer-StatusDoll>

    public HealthAnalyzerUiState() {}

    // <Onyx-HealthAnalyzer-StatusDoll-edited>
    public HealthAnalyzerUiState(
        NetEntity? targetEntity,
        float temperature,
        float bloodLevel,
        bool? scanMode,
        bool? bleeding,
        bool? unrevivable,
        Dictionary<TargetBodyPart, DamageSpecifier>? partDamage = null,
        HealthAnalyzerWoundDiagnostics? woundDiagnostics = null,
        List<HealthAnalyzerOrganInfo>? organs = null,
        List<HealthAnalyzerChemicalInfo>? chemicals = null) // <Onyx-HealthAnalyzerChemicals-edited>
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        // <Onyx-HealthAnalyzer-StatusDoll>
        PartDamage = partDamage;
        WoundDiagnostics = woundDiagnostics;
        // <Onyx-HealthAnalyzerOrgans-edited>
        Organs = organs;
        // </Onyx-HealthAnalyzerOrgans-edited>
        Chemicals = chemicals; // <Onyx-HealthAnalyzerChemicals>
        // </Onyx-HealthAnalyzer-StatusDoll>
    }
    // </Onyx-HealthAnalyzer-StatusDoll-edited>
}
