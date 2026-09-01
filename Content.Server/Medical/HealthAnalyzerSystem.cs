using System.Linq;
using Content.Server.Medical.Components;
// <Onyx-HealthAnalyzer-StatusDoll>
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Medical;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint; // <Onyx-HealthAnalyzerPain>
// </Onyx-HealthAnalyzer-StatusDoll>
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components; // <Onyx-HealthAnalyzerChemicals>
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems; // <Onyx-VitalDamage>
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Content.Server.Body.Systems;

namespace Content.Server.Medical;

public sealed partial class HealthAnalyzerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
// <Onyx-HealthAnalyzer-StatusDoll>
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private PainSystem _pain = default!; // <Onyx-HealthAnalyzerPain>
    [Dependency] private BodyPartFunctionalitySystem _functionality = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    // </Onyx-HealthAnalyzer-StatusDoll>
    [Dependency] private MobThresholdSystem _mobThreshold = default!; // <Onyx-VitalDamage>

    public override void Initialize()
    {
        SubscribeLocalEvent<HealthAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<HealthAnalyzerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<HealthAnalyzerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<HealthAnalyzerComponent, DroppedEvent>(OnDropped);
    }

    public override void Update(float frameTime)
    {
        var analyzerQuery = EntityQueryEnumerator<HealthAnalyzerComponent, TransformComponent>();
        while (analyzerQuery.MoveNext(out var uid, out var component, out var transform))
        {
            //Update rate limited to 1 second
            if (component.NextUpdate > _timing.CurTime)
                continue;

            if (component.ScannedEntity is not {} patient)
                continue;

            if (Deleted(patient))
            {
                StopAnalyzingEntity((uid, component), patient);
                continue;
            }

            component.NextUpdate = _timing.CurTime + component.UpdateInterval;

            //Get distance between health analyzer and the scanned entity
            //null is infinite range
            var patientCoordinates = Transform(patient).Coordinates;
            if (component.MaxScanRange != null && !_transformSystem.InRange(patientCoordinates, transform.Coordinates, component.MaxScanRange.Value))
            {
                //Range too far, disable updates until they are back in range
                PauseAnalyzingEntity((uid, component), patient);
                continue;
            }

            component.IsAnalyzerActive = true;
            UpdateScannedUser(uid, patient, true);
        }
    }

    /// <summary>
    /// Trigger the doafter for scanning
    /// </summary>
    private void OnAfterInteract(Entity<HealthAnalyzerComponent> uid, ref AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !HasComp<MobStateComponent>(args.Target) || !_cell.HasDrawCharge(uid.Owner, user: args.User))
            return;

        _audio.PlayPvs(uid.Comp.ScanningBeginSound, uid);

        var doAfterCancelled = !_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, uid.Comp.ScanDelay, new HealthAnalyzerDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            NeedHand = true,
            BreakOnMove = true,
        });

        if (args.Target == args.User || doAfterCancelled || uid.Comp.Silent)
            return;

        var msg = Loc.GetString("health-analyzer-popup-scan-target", ("user", Identity.Entity(args.User, EntityManager)));
        _popupSystem.PopupEntity(msg, args.Target.Value, args.Target.Value, PopupType.Medium);
    }

    private void OnDoAfter(Entity<HealthAnalyzerComponent> uid, ref HealthAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null || !_cell.HasDrawCharge(uid.Owner, user: args.User))
            return;

        if (!uid.Comp.Silent)
            _audio.PlayPvs(uid.Comp.ScanningEndSound, uid);

        OpenUserInterface(args.User, uid);
        BeginAnalyzingEntity(uid, args.Target.Value); // <Onyx-BodyScanner-edited>
        args.Handled = true;
    }

    /// <summary>
    /// Turn off when placed into a storage item or moved between slots/hands
    /// </summary>
    private void OnInsertedIntoContainer(Entity<HealthAnalyzerComponent> uid, ref EntGotInsertedIntoContainerMessage args)
    {
        if (uid.Comp.ScannedEntity is { } patient)
            _toggle.TryDeactivate(uid.Owner);
    }

    /// <summary>
    /// Disable continuous updates once turned off
    /// </summary>
    private void OnToggled(Entity<HealthAnalyzerComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated && ent.Comp.ScannedEntity is { } patient)
            StopAnalyzingEntity(ent, patient);
    }

    /// <summary>
    /// Turn off the analyser when dropped
    /// </summary>
    private void OnDropped(Entity<HealthAnalyzerComponent> uid, ref DroppedEvent args)
    {
        if (uid.Comp.ScannedEntity is { } patient)
            _toggle.TryDeactivate(uid.Owner);
    }

    private void OpenUserInterface(EntityUid user, EntityUid analyzer)
    {
        if (!_uiSystem.HasUi(analyzer, HealthAnalyzerUiKey.Key))
            return;

        _uiSystem.OpenUi(analyzer, HealthAnalyzerUiKey.Key, user);
    }

    /// <summary>
    /// Mark the entity as having its health analyzed, and link the analyzer to it
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that should receive the updates</param>
    /// <param name="target">The entity to start analyzing</param>
    public void BeginAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target) // <Onyx-BodyScanner-edited>
    {
        //Link the health analyzer to the scanned entity
        healthAnalyzer.Comp.ScannedEntity = target;
        _toggle.TryActivate(healthAnalyzer.Owner);

        UpdateScannedUser(healthAnalyzer, target, true);
    }

    /// <summary>
    /// Remove the analyzer from the active list, and remove the component if it has no active analyzers
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that's receiving the updates</param>
    /// <param name="target">The entity to analyze</param>
    public void StopAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target) // <Onyx-BodyScanner-edited>
    {
        //Unlink the analyzer
        healthAnalyzer.Comp.ScannedEntity = null;

        _toggle.TryDeactivate(healthAnalyzer.Owner);

        UpdateScannedUser(healthAnalyzer, target, false);
    }

    // <Onyx-BodyScanner>
    public void ClearAnalyzedEntity(Entity<HealthAnalyzerComponent> healthAnalyzer)
    {
        healthAnalyzer.Comp.ScannedEntity = null;
        healthAnalyzer.Comp.IsAnalyzerActive = false;
        _toggle.TryDeactivate(healthAnalyzer.Owner);
        _uiSystem.ServerSendUiMessage(
            healthAnalyzer.Owner,
            HealthAnalyzerUiKey.Key,
            new HealthAnalyzerScannedUserMessage(new HealthAnalyzerUiState()));
    }
    // </Onyx-BodyScanner>


    /// <summary>
    /// If the scanner is active, sends one last update and sets it to inactive.
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that's receiving the updates</param>
    /// <param name="target">The entity to analyze</param>
    private void PauseAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target)
    {
        if (!healthAnalyzer.Comp.IsAnalyzerActive)
            return;

        UpdateScannedUser(healthAnalyzer, target, false);
        healthAnalyzer.Comp.IsAnalyzerActive = false;
    }

    /// <summary>
    /// Send an update for the target to the healthAnalyzer
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer</param>
    /// <param name="target">The entity being scanned</param>
    /// <param name="scanMode">True makes the UI show ACTIVE, False makes the UI show INACTIVE</param>
    public void UpdateScannedUser(EntityUid healthAnalyzer, EntityUid target, bool scanMode)
    {
        if (!_uiSystem.HasUi(healthAnalyzer, HealthAnalyzerUiKey.Key)
            || !HasComp<DamageableComponent>(target))
            return;

        var uiState = GetHealthAnalyzerUiState(target);
        uiState.ScanMode = scanMode;
        _uiSystem.ServerSendUiMessage(
            healthAnalyzer,
            HealthAnalyzerUiKey.Key,
            new HealthAnalyzerScannedUserMessage(uiState)
        );
    }

    /// <summary>
    /// Creates a HealthAnalyzerState based on the current state of an entity.
    /// </summary>
    /// <param name="target">The entity being scanned</param>
    /// <returns></returns>
    public HealthAnalyzerUiState GetHealthAnalyzerUiState(EntityUid? target)
    {
        if (!target.HasValue || !HasComp<DamageableComponent>(target))
            return new HealthAnalyzerUiState();

        var entity = target.Value;
        var bodyTemperature = float.NaN;

        if (TryComp<TemperatureComponent>(entity, out var temp))
            bodyTemperature = temp.Temperature;

        var bloodAmount = float.NaN;
        var bleeding = false;
        var unrevivable = false;

        if (TryComp<BloodstreamComponent>(entity, out var bloodstream) &&
            _solutionContainerSystem.ResolveSolution(entity, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out var bloodSolution))
        {
            bloodAmount = _bloodstreamSystem.GetBloodLevel(entity);
            bleeding = bloodstream.BleedAmount > 0;
        }

if (TryComp<UnrevivableComponent>(entity, out var unrevivableComp) && unrevivableComp.Analyzable)
            unrevivable = true;

        // <Onyx-VitalDamage>
        FixedPoint2? vitalDamage = null;
        if (HasComp<WoundHostComponent>(entity))
        {
            var damageable = Comp<DamageableComponent>(entity);
            vitalDamage = _mobThreshold.CheckVitalDamage(entity, damageable);
        }
        // </Onyx-VitalDamage>

        return new HealthAnalyzerUiState(
            GetNetEntity(entity),
            bodyTemperature,
            bloodAmount,
            null,
            bleeding,
            unrevivable,
            // <Onyx-HealthAnalyzer-StatusDoll>
            BuildPartDamage(entity),
            BuildWoundDiagnostics(entity),
            // <Onyx-VitalDamage>
            vitalDamage,
            // </Onyx-VitalDamage>
            // <Onyx-HealthAnalyzerOrgans-edited>
            BuildOrganInfo(entity),
            // </Onyx-HealthAnalyzerOrgans-edited>
            BuildChemicalInfo(entity, bloodstream) // <Onyx-HealthAnalyzerChemicals>
            // </Onyx-HealthAnalyzer-StatusDoll>
        );
    }

    // <Onyx-HealthAnalyzer-StatusDoll>
    public Dictionary<TargetBodyPart, DamageSpecifier>? BuildPartDamage(EntityUid body)
    {
        if (!HasComp<SurgeryTargetComponent>(body))
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

    public HealthAnalyzerWoundDiagnostics? BuildWoundDiagnostics(EntityUid body)
    {
        if (!HasComp<SurgeryTargetComponent>(body))
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
            // <Onyx-HealthAnalyzerPain>
            var pain = TryComp(part, out PainComponent? painComponent)
                ? _pain.GetPain((part, painComponent))
                : FixedPoint2.Zero;
            // </Onyx-HealthAnalyzerPain>

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

            // <Onyx-HealthAnalyzerPain-edited>
            var diagnostic = new HealthAnalyzerWoundDiagnostic(
                fracture,
                fractureTreatment,
                bleedingRate,
                bleedingTreatment,
                scarCount,
                pain,
                wounds,
                _functionality.GetState((part, woundable)),
                internalBleedingRate,
                clottingPhase);
            // </Onyx-HealthAnalyzerPain-edited>
            if (diagnostic.HasFindings)
                result[target] = diagnostic;
        }

        return new HealthAnalyzerWoundDiagnostics(result);
    }

    // <Onyx-HealthAnalyzerOrgans-edited>
    private List<HealthAnalyzerOrganInfo>? BuildOrganInfo(EntityUid body)
    {
        if (!HasComp<SurgeryTargetComponent>(body))
            return null;

        var result = new List<HealthAnalyzerOrganInfo>();
        foreach (var (organ, component) in _body.GetBodyOrgans(body))
        {
            result.Add(new HealthAnalyzerOrganInfo(
                GetNetEntity(organ),
                component.Health,
                component.MaxHealth,
                OrganOrder(component.Category?.Id)));
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
    // </Onyx-HealthAnalyzerOrgans-edited>

    // <Onyx-HealthAnalyzerChemicals>
    private List<HealthAnalyzerChemicalInfo> BuildChemicalInfo(EntityUid body, BloodstreamComponent? bloodstream)
    {
        var result = new List<HealthAnalyzerChemicalInfo>();
        if (bloodstream != null)
        {
            if (_solutionContainerSystem.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var blood))
                AddChemicalInfo(result, HealthAnalyzerSolutionType.Bloodstream, blood);

            if (_solutionContainerSystem.TryGetSolution(body, bloodstream.MetabolitesSolutionName, out _, out var metabolites))
                AddChemicalInfo(result, HealthAnalyzerSolutionType.Metabolites, metabolites);
        }

        foreach (var (organ, _) in _body.GetBodyOrgans(body))
        {
            if (HasComp<StomachComponent>(organ) &&
                _solutionContainerSystem.TryGetSolution(organ, StomachSystem.DefaultSolutionName, out _, out var stomach))
                AddChemicalInfo(result, HealthAnalyzerSolutionType.Stomach, stomach);

            if (TryComp(organ, out LungComponent? lung) &&
                _solutionContainerSystem.TryGetSolution(organ, lung.SolutionName, out _, out var lungs))
                AddChemicalInfo(result, HealthAnalyzerSolutionType.Lung, lungs);
        }

        return result;
    }

    private static void AddChemicalInfo(
        List<HealthAnalyzerChemicalInfo> result,
        HealthAnalyzerSolutionType type,
        Solution solution)
    {
        var reagents = new List<HealthAnalyzerReagentInfo>();
        foreach (var reagent in solution.Contents)
            reagents.Add(new HealthAnalyzerReagentInfo(reagent.Reagent.Prototype, reagent.Quantity));

        result.Add(new HealthAnalyzerChemicalInfo(type, reagents));
    }
    // </Onyx-HealthAnalyzerChemicals>
    // </Onyx-HealthAnalyzer-StatusDoll>
}
