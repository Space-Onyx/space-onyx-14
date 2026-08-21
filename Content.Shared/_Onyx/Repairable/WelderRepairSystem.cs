using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Repairable;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Repairable;

public sealed partial class WelderRepairSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private TargetResolverSystem _targetResolver = default!;
    [Dependency] private WoundDamageRoutingSystem _routing = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WelderRepairModesComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<WelderRepairModesComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
        SubscribeLocalEvent<TargetingComponent, InteractUsingEvent>(OnRepair,
            before: [typeof(RepairableSystem)]);
        SubscribeLocalEvent<TargetingComponent, WelderRepairDoAfterEvent>(OnDoAfter);
    }

    private void OnExamine(Entity<WelderRepairModesComponent> welder, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange && welder.Comp.RepairModes.TryGetValue(welder.Comp.RepairMode, out var mode))
            args.PushMarkup(Loc.GetString("repair-mode-current", ("mode", Loc.GetString(mode.Name))));
    }

    private void OnGetVerbs(Entity<WelderRepairModesComponent> welder, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        foreach (var (key, mode) in welder.Comp.RepairModes)
        {
            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.GetString(mode.Name),
                Category = VerbCategory.SelectType,
                Disabled = key == welder.Comp.RepairMode,
                Act = () => SetMode(welder, key, user),
            });
        }
    }

    private void SetMode(Entity<WelderRepairModesComponent> welder, string key, EntityUid user)
    {
        if (!welder.Comp.RepairModes.TryGetValue(key, out var mode) || welder.Comp.RepairMode == key)
            return;

        welder.Comp.RepairMode = key;
        Dirty(welder);
        _popup.PopupClient(Loc.GetString("repair-mode-changed", ("mode", Loc.GetString(mode.Name))), welder, user);
    }

    private void OnRepair(Entity<TargetingComponent> body, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out WelderComponent? welder) ||
            !TryComp(args.Used, out WelderRepairModesComponent? repairModes) || repairModes.RepairModes.Count == 0)
            return;

        args.Handled = true;
        if (!repairModes.RepairModes.TryGetValue(repairModes.RepairMode, out var mode))
        {
            _popup.PopupClient(Loc.GetString("repair-mode-invalid"), args.Used, args.User);
            return;
        }

        if (!TryComp(args.User, out TargetingComponent? targeting) ||
            !_targetResolver.TryResolveExact(body, targeting.Target, out var part))
        {
            _popup.PopupClient(Loc.GetString("targeting-selected-part-missing"), body, args.User);
            return;
        }

        DamageSpecifier? damage;
        HashSet<TreatmentCapability> treatmentCapabilities;
        float fuelCost;
        ProtoId<Content.Shared.Tools.ToolQualityPrototype> qualityNeeded;
        int doAfterDelay;
        float selfRepairPenalty;
        bool allowSelfRepair;
        if (TryComp(body, out RepairableComponent? repairable))
        {
            damage = repairable.Damage;
            treatmentCapabilities = repairable.TreatmentCapabilities;
            fuelCost = repairable.FuelCost;
            qualityNeeded = repairable.QualityNeeded;
            doAfterDelay = repairable.DoAfterDelay;
            selfRepairPenalty = repairable.SelfRepairPenalty;
            allowSelfRepair = repairable.AllowSelfRepair;
        }
        else if (TryComp(part, out RepairableBodyPartComponent? repairablePart))
        {
            damage = repairablePart.Damage;
            treatmentCapabilities = repairablePart.TreatmentCapabilities;
            fuelCost = repairablePart.FuelCost;
            qualityNeeded = repairablePart.QualityNeeded;
            doAfterDelay = repairablePart.DoAfterDelay;
            selfRepairPenalty = repairablePart.SelfRepairPenalty;
            allowSelfRepair = repairablePart.AllowSelfRepair;
        }
        else
        {
            _popup.PopupClient(Loc.GetString("targeting-selected-part-incompatible"), body, args.User);
            return;
        }

        if (!IsCompatiblePart(body, part, treatmentCapabilities, mode))
        {
            _popup.PopupClient(Loc.GetString("targeting-selected-part-incompatible"), body, args.User);
            return;
        }

        var repair = GetRepair(damage, mode);
        if (!HasRepairableDamage(part, repair))
        {
            _popup.PopupClient(Loc.GetString("targeting-selected-part-healthy"), body, args.User);
            return;
        }

        if (args.User == body.Owner && !allowSelfRepair)
            return;

        var delay = doAfterDelay * Math.Max(0f, mode.DelayMultiplier);
        if (args.User == body.Owner)
            delay *= selfRepairPenalty;

        _tools.UseTool(args.Used, args.User, body, delay, qualityNeeded,
            new WelderRepairDoAfterEvent(GetNetEntity(part), repairModes.RepairMode),
            fuelCost * Math.Max(0f, mode.FuelMultiplier));
    }

    private void OnDoAfter(Entity<TargetingComponent> body, ref WelderRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Used is not { } used || !HasComp<WelderComponent>(used) ||
            !TryComp(used, out WelderRepairModesComponent? repairModes) ||
            !repairModes.RepairModes.TryGetValue(args.Mode, out var mode))
            return;

        var part = GetEntity(args.Part);
        DamageSpecifier? damage;
        HashSet<TreatmentCapability> treatmentCapabilities;
        bool autoDoAfter;
        if (TryComp(body, out RepairableComponent? repairable))
        {
            damage = repairable.Damage;
            treatmentCapabilities = repairable.TreatmentCapabilities;
            autoDoAfter = repairable.AutoDoAfter;
        }
        else if (TryComp(part, out RepairableBodyPartComponent? repairablePart))
        {
            damage = repairablePart.Damage;
            treatmentCapabilities = repairablePart.TreatmentCapabilities;
            autoDoAfter = repairablePart.AutoDoAfter;
        }
        else
            return;

        if (!IsCompatiblePart(body, part, treatmentCapabilities, mode))
            return;

        var repair = GetRepair(damage, mode);
        _routing.TryApplyPartDamage(body, part, repair, args.User, healWounds: false);
        _wounds.TryHealWounds(part, repair);
        args.Repeat = autoDoAfter && HasRepairableDamage(part, repair);
        args.Args.Event.Repeat = args.Repeat;
        args.Handled = true;
    }

    private bool IsCompatiblePart(EntityUid body, EntityUid part, HashSet<TreatmentCapability> treatmentCapabilities,
        WelderRepairMode mode)
    {
        return _body.BodyHasChild(body, part) && HasComp<DamageableComponent>(part) &&
               TryComp(part, out WoundableComponent? woundable) &&
               _prototypes.TryIndex(woundable.Profile, out var profile) &&
               profile.TreatmentCapabilities.Overlaps(treatmentCapabilities) &&
               profile.TreatmentCapabilities.Overlaps(mode.TreatmentCapabilities);
    }

    private DamageSpecifier GetRepair(DamageSpecifier? damage, WelderRepairMode mode)
    {
        if (damage == null)
            return new DamageSpecifier();

        var repair = new DamageSpecifier();
        if (!_prototypes.TryIndex(mode.DamageGroup, out DamageGroupPrototype? group))
            return repair;

        foreach (var type in group.DamageTypes)
        {
            if (damage.DamageDict.TryGetValue(type, out var amount))
                repair.DamageDict[type] = amount * Math.Max(0f, mode.RepairMultiplier);
        }
        return repair;
    }

    private bool HasRepairableDamage(EntityUid part, DamageSpecifier repair)
    {
        if (_wounds.GetHealingPotential(part, repair) > 0 || !TryComp(part, out DamageableComponent? damageable))
            return _wounds.GetHealingPotential(part, repair) > 0;

        foreach (var (type, amount) in repair.DamageDict)
        {
            if (amount < 0 && _damage.GetPositiveDamage((part, damageable)).DamageDict.GetValueOrDefault(type) > 0)
                return true;
        }
        return false;
    }
}

[Serializable, NetSerializable]
public sealed partial class WelderRepairDoAfterEvent : SimpleDoAfterEvent
{
    public readonly NetEntity Part;
    public readonly string Mode;

    public WelderRepairDoAfterEvent(NetEntity part, string mode)
    {
        Part = part;
        Mode = mode;
    }
}
