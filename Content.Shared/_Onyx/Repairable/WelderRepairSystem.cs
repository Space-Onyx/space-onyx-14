using System.Linq;
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
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private TargetResolverSystem _targets = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private WoundDamageProjectionSystem _projection = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WelderRepairModesComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<WelderRepairModesComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
        SubscribeLocalEvent<WoundHostComponent, InteractUsingEvent>(OnRepair);
        SubscribeLocalEvent<WoundHostComponent, WelderRepairDoAfterEvent>(OnDoAfter);
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

        foreach (var (key, mode) in welder.Comp.RepairModes)
        {
            var selected = key;
            var user = args.User;
            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.GetString(mode.Name),
                Category = VerbCategory.SelectType,
                Disabled = key == welder.Comp.RepairMode,
                Act = () => SetMode(welder, selected, user),
            });
        }
    }

    private void SetMode(Entity<WelderRepairModesComponent> welder, string mode, EntityUid user)
    {
        if (!welder.Comp.RepairModes.TryGetValue(mode, out var selected) || welder.Comp.RepairMode == mode)
            return;

        welder.Comp.RepairMode = mode;
        Dirty(welder);
        _popup.PopupClient(Loc.GetString("repair-mode-changed", ("mode", Loc.GetString(selected.Name))), welder, user);
    }

    private void OnRepair(Entity<WoundHostComponent> body, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<WelderComponent>(args.Used) ||
            !TryComp(args.Used, out WelderRepairModesComponent? modes) || modes.RepairModes.Count == 0)
            return;

        args.Handled = true;
        if (!modes.RepairModes.TryGetValue(modes.RepairMode, out var mode) ||
            !TryComp(args.User, out TargetingComponent? targeting) ||
            !_targets.TryResolveExact(body, targeting.Target, out var part))
        {
            _popup.PopupClient(Loc.GetString("targeting-selected-part-missing"), body, args.User);
            return;
        }

        if (!TryGetSettings(body, part, out var settings) || !IsCompatible(body, part, settings.Capabilities, mode))
        {
            _popup.PopupClient(Loc.GetString("targeting-selected-part-incompatible"), body, args.User);
            return;
        }

        var repair = BuildRepair(settings.Damage, mode);
        if (!HasRepairableDamage(part, repair))
        {
            _popup.PopupClient(Loc.GetString("targeting-selected-part-healthy"), body, args.User);
            return;
        }

        if (args.User == body.Owner && !settings.AllowSelfRepair)
            return;

        var delay = settings.Delay * Math.Max(0f, mode.DelayMultiplier);
        if (args.User == body.Owner)
            delay *= settings.SelfRepairPenalty;

        _tools.UseTool(args.Used, args.User, body, delay, settings.Quality,
            new WelderRepairDoAfterEvent(GetNetEntity(part), modes.RepairMode),
            settings.Fuel * Math.Max(0f, mode.FuelMultiplier));
    }

    private void OnDoAfter(Entity<WoundHostComponent> body, ref WelderRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Used is not { } used || !HasComp<WelderComponent>(used) ||
            !TryComp(used, out WelderRepairModesComponent? modes) ||
            !modes.RepairModes.TryGetValue(args.Mode, out var mode))
            return;

        var part = GetEntity(args.Part);
        if (!_body.BodyHasChild(body, part) || !TryComp(part, out DamageableComponent? damageable) ||
            !TryGetSettings(body, part, out var settings) ||
            !IsCompatible(body, part, settings.Capabilities, mode))
            return;

        var repair = BuildRepair(settings.Damage, mode);
        if (_damage.TryChangeDamage((part, damageable), repair, out var applied,
                ignoreResistances: true, interruptsDoAfters: false, ignoreGlobalModifiers: true))
        {
            var damageApplied = new PartDamageAppliedEvent(body, part, applied, HealWounds: false);
            RaiseLocalEvent(part, ref damageApplied);
        }

        _wounds.TryHealWounds(part, repair);
        _projection.RefreshBodyDamage(body);
        args.Repeat = settings.AutoDoAfter && HasRepairableDamage(part, repair);
        args.Args.Event.Repeat = args.Repeat;
        args.Handled = true;
    }

    private bool TryGetSettings(EntityUid body, EntityUid part, out RepairSettings settings)
    {
        if (TryComp(part, out RepairableBodyPartComponent? partRepair))
        {
            settings = new(partRepair.Damage, partRepair.TreatmentCapabilities, partRepair.FuelCost,
                partRepair.QualityNeeded, partRepair.DoAfterDelay, partRepair.SelfRepairPenalty,
                partRepair.AllowSelfRepair, partRepair.AutoDoAfter);
            return true;
        }

        if (TryComp(body, out RepairableComponent? bodyRepair))
        {
            settings = new(bodyRepair.Damage, bodyRepair.TreatmentCapabilities, bodyRepair.FuelCost,
                bodyRepair.QualityNeeded, bodyRepair.DoAfterDelay, bodyRepair.SelfRepairPenalty,
                bodyRepair.AllowSelfRepair, bodyRepair.AutoDoAfter);
            return true;
        }

        settings = default;
        return false;
    }

    private bool IsCompatible(EntityUid body, EntityUid part, HashSet<TreatmentCapability> capabilities,
        WelderRepairMode mode)
    {
        return _body.BodyHasChild(body, part) && TryComp(part, out WoundableComponent? woundable) &&
               _prototypes.TryIndex(woundable.Profile, out var profile) &&
               profile.TreatmentCapabilities.Overlaps(capabilities) &&
               profile.TreatmentCapabilities.Overlaps(mode.TreatmentCapabilities);
    }

    private DamageSpecifier BuildRepair(DamageSpecifier? damage, WelderRepairMode mode)
    {
        var repair = new DamageSpecifier();
        if (damage == null || !_prototypes.TryIndex(mode.DamageGroup, out DamageGroupPrototype? group))
            return repair;

        foreach (var type in group.DamageTypes)
            if (damage.DamageDict.TryGetValue(type, out var amount))
                repair.DamageDict[type] = amount * Math.Max(0f, mode.RepairMultiplier);
        return repair;
    }

    private bool HasRepairableDamage(EntityUid part, DamageSpecifier repair)
    {
        if (_wounds.GetHealingPotential(part, repair) > 0)
            return true;

        if (!TryComp(part, out DamageableComponent? damageable))
            return false;

        var current = _damage.GetPositiveDamage((part, damageable));
        return repair.DamageDict.Any(pair => pair.Value < 0 && current.DamageDict.GetValueOrDefault(pair.Key) > 0);
    }

    private readonly record struct RepairSettings(
        DamageSpecifier? Damage,
        HashSet<TreatmentCapability> Capabilities,
        float Fuel,
        ProtoId<Content.Shared.Tools.ToolQualityPrototype> Quality,
        int Delay,
        float SelfRepairPenalty,
        bool AllowSelfRepair,
        bool AutoDoAfter);
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
