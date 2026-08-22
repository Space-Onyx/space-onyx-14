using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared._Onyx.Targeting; // <Onyx-WoundTreatment>
using Content.Shared._Onyx.Wounds; // <Onyx-WoundTreatment>
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Medical.Healing;

public sealed partial class HealingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private SharedBodySystem _bodySystem = default!; // <Onyx-TargetedHealingFeedback>
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private TargetResolverSystem _targetResolver = default!;
    [Dependency] private WoundHealingSystem _woundHealing = default!; // Onyx-WoundSystem-edited

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealingComponent, UseInHandEvent>(OnHealingUse);
        SubscribeLocalEvent<HealingComponent, AfterInteractEvent>(OnHealingAfterInteract);
        SubscribeLocalEvent<DamageableComponent, HealingDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(Entity<DamageableComponent> target, ref HealingDoAfterEvent args)
    {

        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp(args.Used, out HealingComponent? healing))
            return;

        // <Onyx-WoundTreatment>
        // Onyx-WoundSystem-edited: localized healing belongs to the selected part, never the body projection.
        if (HasComp<WoundHostComponent>(target))
        {
            EntityUid? requestedPart = args.RequestedPart is { } netPart ? GetEntity(netPart) : null;
            if (requestedPart is { } concretePart && _woundHealing.ResolveHealingPart(target, concretePart,
                    healing.Damage, healing.DamageContainers, healing.TreatmentCapabilities,
                    healing.AllowedWoundStages, healing.BloodlossModifier, healing.HealWounds) != concretePart)
            {
                var message = _bodySystem.BodyHasChild(target, concretePart)
                    ? "targeting-selected-part-incompatible"
                    : "targeting-selected-part-missing";
                _popupSystem.PopupClient(Loc.GetString(message), target, args.User);
                return;
            }

            if (!_woundHealing.TryApplyHealing(target, requestedPart, (args.Used.Value, healing), args.User,
                    out var woundHealed, out var stoppedBleeding))
                return;

            TryComp<BloodstreamComponent>(target, out var woundBloodstream);
            if (healing.ModifyBloodLevel != 0 && woundBloodstream != null)
                _bloodstreamSystem.TryModifyBloodLevel((target.Owner, woundBloodstream), healing.ModifyBloodLevel);

            if (stoppedBleeding)
            {
                var popup = args.User == target.Owner
                    ? Loc.GetString("medical-item-stop-bleeding-self")
                    : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target.Owner, EntityManager)));
                _popupSystem.PopupClient(popup, target, args.User);
            }

            FinishHealing(target, ref args, healing, woundHealed);
            return;
        }
        // </Onyx-WoundTreatment>

        if (!TryComp<InjurableComponent>(target, out var injurable))
            return;

        if (healing.DamageContainers is not null &&
            injurable.DamageContainer is not null &&
            !healing.DamageContainers.Contains(injurable.DamageContainer.Value))
        {
            return;
        }

        TryComp<BloodstreamComponent>(target, out var bloodstream);

        // Heal some bloodloss damage.
        if (healing.BloodlossModifier != 0 && bloodstream != null)
        {
            var isBleeding = bloodstream.BleedAmount > 0;
            _bloodstreamSystem.TryModifyBleedAmount((target.Owner, bloodstream), healing.BloodlossModifier);
            if (isBleeding != bloodstream.BleedAmount > 0)
            {
                var popup = (args.User == target.Owner)
                    ? Loc.GetString("medical-item-stop-bleeding-self")
                    : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target.Owner, EntityManager)));
                _popupSystem.PopupEntity(popup, target, args.User);
            }
        }

        // Restores missing blood
        if (healing.ModifyBloodLevel != 0 && bloodstream != null)
            _bloodstreamSystem.TryModifyBloodLevel((target.Owner, bloodstream), healing.ModifyBloodLevel);

        if (!_damageable.TryChangeDamage(target.Owner, healing.Damage * _damageable.UniversalTopicalsHealModifier, out var healed, true, origin: args.Args.User) && healing.BloodlossModifier != 0)
            return;

        FinishHealing(target, ref args, healing, healed);
    }

    private void FinishHealing(Entity<DamageableComponent> target, ref HealingDoAfterEvent args,
        HealingComponent healing, DamageSpecifier healed)
    {
        if (args.Used is not { } used)
            return;

        var total = healed.GetTotal();

        // Re-verify that we can heal the damage.
        var dontRepeat = false;
        if (TryComp<StackComponent>(used, out var stackComp))
        {
            _stacks.ReduceCount((used, stackComp), 1);

            if (_stacks.GetCount((used, stackComp)) <= 0)
                dontRepeat = true;
        }
        else
        {
            PredictedQueueDel(used);
        }

        if (target.Owner != args.User)
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed {ToPrettyString(target.Owner):target} for {total:damage} damage");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed themselves for {total:damage} damage");
        }

        _audio.PlayPredicted(healing.HealingEndSound, target.Owner, args.User);

        // Logic to determine the whether or not to repeat the healing action
        var requestedPart = args.RequestedPart is { } netPart ? GetEntity(netPart) : (EntityUid?) null;
        args.Repeat = HasDamage((used, healing), target, requestedPart) && !dontRepeat;
        args.Handled = true;

        if (!args.Repeat)
        {
            _popupSystem.PopupEntity(Loc.GetString("medical-item-finished-using", ("item", args.Used)), target.Owner, args.User);
            return;
        }

        // Update our self heal delay so it shortens as we heal more damage.
        if (args.User == target.Owner)
            args.Args.Delay = healing.Delay * GetScaledHealingPenalty(target.Owner, healing.SelfHealPenaltyMultiplier);
    }

    private bool HasDamage(Entity<HealingComponent> healing, Entity<DamageableComponent> target,
        EntityUid? requestedPart = null)
    {
        // <Onyx-WoundSystem-edited>
        if (TryComp(target, out WoundHostComponent? host))
        {
            var resolve = new ResolveHealingPartEvent(target, healing.Comp.Damage, healing.Comp.DamageContainers,
                healing.Comp.TreatmentCapabilities, healing.Comp.AllowedWoundStages,
                healing.Comp.BloodlossModifier, requestedPart, healing.Comp.HealWounds);
            RaiseLocalEvent(target, ref resolve);
            if (!resolve.Accepted)
                return false;

            if (healing.Comp.HealDamage)
            {
                foreach (var (type, amount) in healing.Comp.Damage.DamageDict)
                {
                    var source = host.LocalizedDamageTypes.Contains(type) ? resolve.Part : target.Owner;
                    if (amount < 0 && source is { } entity &&
                        _damageable.GetAllDamage(entity).DamageDict.GetValueOrDefault(type) > 0)
                        return true;
                }
            }

            if (healing.Comp.HealWounds && resolve.Part is { } woundPart &&
                _woundHealing.HasTreatableWounds(woundPart, healing.Comp.Damage, healing.Comp.AllowedWoundStages))
                return true;

            if (resolve.Part is { } bleedingPart && healing.Comp.BloodlossModifier < 0 &&
                _woundHealing.CanTreatBleeding(bleedingPart))
                return true;

            if (TryComp<BloodstreamComponent>(target, out var hostBloodstream) &&
                healing.Comp.ModifyBloodLevel > 0 &&
                _solutionContainerSystem.ResolveSolution(target.Owner, hostBloodstream.BloodSolutionName,
                    ref hostBloodstream.BloodSolution, out _) &&
                _bloodstreamSystem.GetBloodLevel((target, hostBloodstream)) < 1)
                return true;

            return false;
        }
        // </Onyx-WoundSystem-edited>

        var damageableDict = _damageable.GetAllDamage(target.AsNullable()).DamageDict;
        var healingDict = healing.Comp.Damage.DamageDict;
        foreach (var type in healingDict)
        {
            if (damageableDict.TryGetValue(type.Key, out var amount) && amount > 0)
            {
                return true;
            }
        }

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            // Is ent missing blood that we can restore?
            if (healing.Comp.ModifyBloodLevel > 0
                && _solutionContainerSystem.ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
                && _bloodstreamSystem.GetBloodLevel((target, bloodstream)) < 1)
            {
                return true;
            }

            // Is ent bleeding and can we stop it?
            if (healing.Comp.BloodlossModifier < 0 && bloodstream.BleedAmount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void OnHealingUse(Entity<HealingComponent> healing, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryHealTargeted(healing, args.User, args.User))
        {
            args.Handled = true;
            return;
        }

        if (TryHeal(healing, args.User, args.User))
            args.Handled = true;
    }

    private void OnHealingAfterInteract(Entity<HealingComponent> healing, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (TryHealTargeted(healing, args.Target.Value, args.User))
        {
            args.Handled = true;
            return;
        }

        if (TryHeal(healing, args.Target.Value, args.User))
            args.Handled = true;
    }

    private bool TryHealTargeted(Entity<HealingComponent> healing, EntityUid target, EntityUid user)
    {
        if (!HasComp<WoundHostComponent>(target) || !TryComp(user, out TargetingComponent? targeting))
            return false;

        if (!_targetResolver.TryResolveExact(target, targeting.Target, out var part))
        {
            _popupSystem.PopupClient(Loc.GetString("targeting-selected-part-missing"), target, user);
            return true;
        }

        // <Onyx-TargetedHealingFeedback>
        if (!_woundHealing.IsCompatiblePart(target, part, healing.Comp.DamageContainers,
                healing.Comp.TreatmentCapabilities))
        {
            _popupSystem.PopupClient(Loc.GetString("targeting-selected-part-incompatible"), target, user);
            return true;
        }
        // </Onyx-TargetedHealingFeedback>

        TryHeal(healing, target, user, part);
        return true;
    }

    public bool TryHeal(Entity<HealingComponent> healing, Entity<DamageableComponent?> target, EntityUid user,
        EntityUid? requestedPart = null)
    {
        if (!Resolve(target, ref target.Comp, false))
            return false;

        if (!TryComp<InjurableComponent>(target, out var injurable))
            return false;

        // <Onyx-WoundTreatment>
        // Onyx-WoundSystem-edited: public explicit-part entry point for future Targeting.
        var woundHost = HasComp<WoundHostComponent>(target);
        var resolvedPart = false;
        if (woundHost)
        {
            var resolve = new ResolveHealingPartEvent(target, healing.Comp.Damage, healing.Comp.DamageContainers,
                healing.Comp.TreatmentCapabilities, healing.Comp.AllowedWoundStages,
                healing.Comp.BloodlossModifier, requestedPart, healing.Comp.HealWounds);
            RaiseLocalEvent(target, ref resolve);
            if (!resolve.Accepted)
                return false;
            resolvedPart = resolve.Part != null;
        }
        // </Onyx-WoundTreatment>

        if (!resolvedPart && healing.Comp.DamageContainers is not null &&
            injurable.DamageContainer is not null &&
            !healing.Comp.DamageContainers.Contains(injurable.DamageContainer.Value))
        {
            return false;
        }

        if (user != target.Owner && !_interactionSystem.InRangeUnobstructed(user, target.Owner, popup: true))
            return false;

        if (TryComp<StackComponent>(healing, out var stack) && stack.Count < 1)
            return false;

        if (!HasDamage(healing, target!, requestedPart))
        {
            _popupSystem.PopupEntity(Loc.GetString("medical-item-cant-use", ("item", healing.Owner)), healing, user);
            return false;
        }

        _audio.PlayPredicted(healing.Comp.HealingBeginSound, healing, user);

        var isNotSelf = user != target.Owner;

        if (isNotSelf)
        {
            var msg = Loc.GetString("medical-item-popup-target", ("user", Identity.Entity(user, EntityManager)), ("item", healing.Owner));
            _popupSystem.PopupEntity(msg, target, target, PopupType.Medium);
        }

        var delay = isNotSelf
            ? healing.Comp.Delay
            : healing.Comp.Delay * GetScaledHealingPenalty(target, healing.Comp.SelfHealPenaltyMultiplier);

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, delay,
                new HealingDoAfterEvent(requestedPart is { } part ? GetNetEntity(part) : null),
                target, target: target, used: healing)
            {
                // Didn't break on damage as they may be trying to prevent it and
                // not being able to heal your own ticking damage would be frustrating.
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }

    /// <summary>
    /// Scales the self-heal penalty based on the amount of damage taken
    /// </summary>
    /// <param name="ent">Entity we're healing</param>
    /// <param name="mod">Maximum modifier we can have.</param>
    /// <returns>Modifier we multiply our healing time by</returns>
    public float GetScaledHealingPenalty(Entity<DamageableComponent?, MobThresholdsComponent?> ent, float mod)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, false))
            return mod;

        if (!_mobThresholdSystem.TryGetThresholdForState(ent, MobState.Critical, out var amount, ent.Comp2))
            return 1;

        var percentDamage = (float)(_damageable.GetTotalDamage(ent) / amount);
        //basically make it scale from 1 to the multiplier.

        var output = percentDamage * (mod - 1) + 1;
        return Math.Max(output, 1);
    }
}
