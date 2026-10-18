using System.Linq;
using Content.Shared._Onyx.Medical;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;

namespace Content.Shared.Medical.HealthAnalyzer;

public abstract partial class HealthAnalyzerSystem
{
    // Body-part, wound, organ and reagent diagnostics shared by predicted scanner updates.
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private BodyPartFunctionalitySystem _functionality = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private INetManager _net = default!;

    /// <summary>
    /// Sets the additional charge consumed when a scan completes.
    /// </summary>
    public void SetScanCharge(Entity<HealthAnalyzerComponent> analyzer, float charge)
    {
        analyzer.Comp.ScanCharge = charge;
        DirtyField(analyzer.AsNullable(), nameof(HealthAnalyzerComponent.ScanCharge));
    }

    private void PopulateDiagnostics(HealthAnalyzerUiState state, EntityUid entity)
    {
        state.PartDamage = BuildPartDamage(entity);
        state.WoundDiagnostics = BuildWoundDiagnostics(entity);
        if (HasComp<WoundHostComponent>(entity) && TryComp<DamageableComponent>(entity, out var damageable))
            state.VitalDamage = _mobThreshold.CheckVitalDamage(entity, damageable);
        state.Organs = BuildOrganInfo(entity);
        state.Chemicals = BuildChemicalInfo(entity, CompOrNull<BloodstreamComponent>(entity));
    }

    private void PreserveAuthoritativeDiagnostics(Entity<HealthAnalyzerComponent> analyzer, HealthAnalyzerUiState state)
    {
        if (!_net.IsClient || state.TargetEntity == null ||
            !_ui.TryGetUiState<HealthAnalyzerUiState>(analyzer.Owner, HealthAnalyzerUiKey.Key, out var previous) ||
            previous.TargetEntity != state.TargetEntity)
            return;

        // Clotting timers and solution contents are not all replicated to the client.
        state.WoundDiagnostics = previous.WoundDiagnostics;
        state.Organs = previous.Organs;
        state.Chemicals = previous.Chemicals;
        state.VitalDamage = previous.VitalDamage;
    }

    /// <summary>
    /// Collects damage for each selectable humanoid body part.
    /// </summary>
    public Dictionary<TargetBodyPart, DamageSpecifier>? BuildPartDamage(EntityUid body)
    {
        if (!HasComp<BodyComponent>(body) || !HasComp<HumanoidProfileComponent>(body))
            return null;

        var result = new Dictionary<TargetBodyPart, DamageSpecifier>();
        foreach (var (part, bodyPart) in _body.GetBodyChildren(body))
        {
            if (!TryComp(part, out DamageableComponent? damageable) ||
                !SharedTargetingSystem.TryConvert(bodyPart.PartType, bodyPart.Symmetry, out var target))
                continue;

            var damage = new DamageSpecifier(_damageable.GetAllDamage((part, damageable)));
            result[target] = damage;
            if (target == TargetBodyPart.Chest)
                result[TargetBodyPart.Groin] = new DamageSpecifier(damage);
        }

        return result;
    }

    /// <summary>
    /// Collects wounds, fractures, bleeding, pain and nerve damage for each body part.
    /// </summary>
    public HealthAnalyzerWoundDiagnostics? BuildWoundDiagnostics(EntityUid body)
    {
        if (!HasComp<BodyComponent>(body))
            return null;

        var result = new Dictionary<TargetBodyPart, HealthAnalyzerWoundDiagnostic>();
        foreach (var (part, bodyPart) in _body.GetBodyChildren(body))
        {
            if (!TryComp(part, out WoundableComponent? woundable) ||
                !SharedTargetingSystem.TryConvert(bodyPart.PartType, bodyPart.Symmetry, out var target))
                continue;

            var fracture = FractureGrade.None;
            var fractureTreatment = FractureTreatment.None;
            var bleedingRate = 0f;
            var bleedingTreatment = BleedingTreatment.None;
            var highestBleedingRate = 0f;
            ushort scarCount = 0;
            var visibleWounds = new Dictionary<(LocId Name, LocId? StageName), int>();
            var clottingPhases = new HashSet<HealthAnalyzerClottingPhase>();
            var internalBleedingRate = 0f;
            var nerve = CompOrNull<NerveComponent>(part);
            var nerveDamage = nerve?.Damage ?? FixedPoint2.Zero;
            var nerveMaxDamage = nerve?.MaxDamage ?? FixedPoint2.Zero;
            var pain = _pain.GetPartPain(part);

            foreach (var wound in _wounds.GetWounds((part, woundable)))
            {
                if (TryComp(wound, out WoundFractureComponent? foundFracture) &&
                    foundFracture.Treatment != FractureTreatment.Mended &&
                    foundFracture.Grade > fracture)
                {
                    fracture = foundFracture.Grade;
                    fractureTreatment = foundFracture.Treatment;
                }

                if (TryComp(wound, out WoundBleedingComponent? foundBleeding) && foundBleeding.CurrentRate > 0f)
                {
                    bleedingRate += foundBleeding.CurrentRate;
                    if (foundBleeding.CurrentRate > highestBleedingRate)
                    {
                        highestBleedingRate = foundBleeding.CurrentRate;
                        bleedingTreatment = foundBleeding.Treatment;
                    }
                }

                if (HasComp<WoundScarComponent>(wound))
                    scarCount++;

                if (TryComp(wound, out WoundInternalBleedingComponent? internalBleeding) &&
                    wound.Comp.State == WoundState.Open && internalBleeding.Severity > FixedPoint2.Zero)
                    internalBleedingRate += internalBleeding.Rate * internalBleeding.Severity.Float();

                if (TryComp(wound, out WoundBleedingComponent? clotting) && wound.Comp.State == WoundState.Open)
                {
                    clottingPhases.Add(clotting.AutomaticClottingAt != null
                        ? HealthAnalyzerClottingPhase.InProgress
                        : clotting.NaturalClotting > 0f && clotting.CurrentRate <= 0f
                            ? HealthAnalyzerClottingPhase.Complete
                            : HealthAnalyzerClottingPhase.None);
                }

                if (!_prototypes.TryIndex(wound.Comp.Prototype, out WoundPrototype? prototype) ||
                    prototype.Visibility != WoundVisibility.Visible ||
                    wound.Comp.State is WoundState.Healed or WoundState.Scarred)
                    continue;

                var stageName = prototype.GetStageDefinition(wound.Comp.Severity)?.Name;
                var key = (prototype.Name, stageName);
                visibleWounds[key] = visibleWounds.GetValueOrDefault(key) + 1;
            }

            var wounds = visibleWounds
                .OrderBy(entry => entry.Key.Name)
                .ThenBy(entry => entry.Key.StageName)
                .Select(entry => new HealthAnalyzerVisibleWound(entry.Key.Name, entry.Key.StageName, entry.Value))
                .ToList();
            var clottingPhase = clottingPhases.Count switch
            {
                0 => HealthAnalyzerClottingPhase.NotApplicable,
                1 => clottingPhases.Single(),
                _ => HealthAnalyzerClottingPhase.Mixed,
            };

            var diagnostic = new HealthAnalyzerWoundDiagnostic(
                fracture, fractureTreatment, bleedingRate, bleedingTreatment, scarCount, pain, wounds,
                _functionality.GetState((part, woundable)), internalBleedingRate, clottingPhase,
                nerveDamage, nerveMaxDamage);
            if (diagnostic.HasFindings)
                result[target] = diagnostic;
        }

        return new HealthAnalyzerWoundDiagnostics(result);
    }

    private List<HealthAnalyzerOrganInfo>? BuildOrganInfo(EntityUid body)
    {
        if (!HasComp<BodyComponent>(body))
            return null;

        var result = new List<HealthAnalyzerOrganInfo>();
        foreach (var (organ, component) in _body.GetBodyOrgans(body))
        {
            result.Add(new HealthAnalyzerOrganInfo(GetNetEntity(organ), component.Health, component.MaxHealth,
                component.MinimumFunctionalHealth, OrganOrder(component.Category?.Id)));
        }

        result.Sort((left, right) => left.Order.CompareTo(right.Order));
        return result;
    }

    private static int OrganOrder(string? category) => category switch
    {
        "Brain" => 0,
        "Eyes" => 1,
        "Ears" => 2,
        "Tongue" => 3,
        "Lungs" => 4,
        "Heart" => 5,
        "Liver" => 6,
        "Stomach" => 7,
        "Appendix" => 8,
        "Kidneys" => 9,
        _ => 10,
    };

    private List<HealthAnalyzerChemicalInfo> BuildChemicalInfo(EntityUid body, BloodstreamComponent? bloodstream)
    {
        var result = new List<HealthAnalyzerChemicalInfo>();
        if (bloodstream != null)
        {
            if (_solutionContainer.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var blood))
                AddChemicalInfo(result, HealthAnalyzerSolutionType.Bloodstream, blood);
            if (_solutionContainer.TryGetSolution(body, bloodstream.MetabolitesSolutionName, out _, out var metabolites))
                AddChemicalInfo(result, HealthAnalyzerSolutionType.Metabolites, metabolites);
        }

        foreach (var (organ, _) in _body.GetBodyOrgans(body))
        {
            if (HasComp<StomachComponent>(organ) &&
                _solutionContainer.TryGetSolution(organ, StomachSystem.DefaultSolutionName, out _, out var stomach))
                AddChemicalInfo(result, HealthAnalyzerSolutionType.Stomach, stomach);
            if (TryComp(organ, out LungComponent? lung) &&
                _solutionContainer.TryGetSolution(organ, lung.SolutionName, out _, out var lungs))
                AddChemicalInfo(result, HealthAnalyzerSolutionType.Lung, lungs);
        }

        return result;
    }

    private static void AddChemicalInfo(List<HealthAnalyzerChemicalInfo> result,
        HealthAnalyzerSolutionType type, Solution solution)
    {
        var reagents = new List<HealthAnalyzerReagentInfo>();
        foreach (var reagent in solution.Contents)
            reagents.Add(new HealthAnalyzerReagentInfo(reagent.Reagent.Prototype, reagent.Quantity));
        result.Add(new HealthAnalyzerChemicalInfo(type, reagents));
    }
}
