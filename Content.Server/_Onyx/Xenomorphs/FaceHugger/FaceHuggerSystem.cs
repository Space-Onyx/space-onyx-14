using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared._Onyx.Xenomorphs.FaceHugger;
using Content.Shared._Onyx.Xenomorphs.Infection;
using Content.Shared._Onyx.Clothing.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Xenomorphs.FaceHugger;

public sealed partial class FaceHuggerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReactiveSystem _reactive = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StunSystem _stun = default!;
    [Dependency] private SharedBodySystem _body = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FaceHuggerComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<FaceHuggerComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FaceHuggerComponent, GotEquippedHandEvent>(OnPickedUp);
        SubscribeLocalEvent<FaceHuggerComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<FaceHuggerComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<FaceHuggerComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<FaceHuggerComponent, ThrowDoHitEvent>(OnThrownHit);
    }

    private void OnCollide(EntityUid uid, FaceHuggerComponent component, StartCollideEvent args)
        => TryEquip(uid, args.OtherEntity, component);

    private void OnMeleeHit(EntityUid uid, FaceHuggerComponent component, MeleeHitEvent args)
    {
        if (args.HitEntities.FirstOrNull() is { } target)
            TryEquip(uid, target, component);
    }

    private void OnPickedUp(EntityUid uid, FaceHuggerComponent component, GotEquippedHandEvent args)
        => TryEquip(uid, args.User, component);

    private void OnStepTriggered(EntityUid uid, FaceHuggerComponent component, ref StepTriggeredOffEvent args)
        => TryEquip(uid, args.Tripper, component);

    private void OnThrownHit(EntityUid uid, FaceHuggerComponent component, ref ThrowDoHitEvent args)
    {
        component.Active = true;
        TryEquip(uid, args.Target, component);
    }

    private void OnEquipped(EntityUid uid, FaceHuggerComponent component, GotEquippedEvent args)
    {
        if (args.Slot != component.Slot || _mobState.IsDead(uid) ||
            HasComp<FaceHuggerImmuneComponent>(args.EquipTarget) ||
            !_whitelist.CheckBoth(args.EquipTarget, component.Blacklist))
            return;

        _popup.PopupEntity(Loc.GetString("xenomorphs-face-hugger-equip", ("equipment", uid)), uid, args.EquipTarget);
        _popup.PopupEntity(
            Loc.GetString("xenomorphs-face-hugger-equip-other",
                ("equipment", uid),
                ("target", Identity.Entity(args.EquipTarget, EntityManager))),
            uid,
            Filter.PvsExcept(args.EquipTarget),
            true);
        _stun.TryKnockdown(args.EquipTarget, component.KnockdownTime, true);

        if (component.InfectionPrototype != null)
        {
            EnsureComp<XenomorphPreventSuicideComponent>(args.EquipTarget);
            component.InfectIn = _timing.CurTime + _random.Next(component.MinInfectTime, component.MaxInfectTime);
        }
    }

    private void OnUnequipAttempt(EntityUid uid, FaceHuggerComponent component, BeingUnequippedAttemptEvent args)
    {
        if (component.InfectionPrototype == null || component.Slot != args.Slot ||
            _mobState.IsDead(uid) ||
            HasComp<FaceHuggerImmuneComponent>(args.UnEquipTarget))
            return;

        _popup.PopupEntity(
            Loc.GetString("xenomorphs-face-hugger-unequip", ("equipment", Identity.Entity(uid, EntityManager))),
            uid,
            args.UnEquipTarget);
        args.Cancel();
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<FaceHuggerComponent, ClothingComponent>();
        while (query.MoveNext(out var uid, out var faceHugger, out var clothing))
        {
            if (!faceHugger.Active && time > faceHugger.RestIn)
                faceHugger.Active = true;

            if (faceHugger.InfectIn != TimeSpan.Zero && time >= faceHugger.InfectIn)
            {
                faceHugger.InfectIn = TimeSpan.Zero;
                Infect(uid, faceHugger);
            }

            if (clothing.InSlot != null && !_mobState.IsDead(uid))
            {
                if (faceHugger.NextInjectionTime == TimeSpan.Zero)
                    faceHugger.NextInjectionTime = time + faceHugger.InitialInjectionDelay;
                else if (time >= faceHugger.NextInjectionTime &&
                         _container.TryGetContainingContainer(uid, out var container) &&
                         container.Owner != uid)
                {
                    if (!HasComp<FaceHuggerImmuneComponent>(container.Owner))
                        Inject(faceHugger, container.Owner);
                    faceHugger.NextInjectionTime = time + faceHugger.InjectionInterval;
                }
            }
            else
            {
                faceHugger.NextInjectionTime = TimeSpan.Zero;
                if (!faceHugger.Active)
                    continue;

                foreach (var target in _lookup.GetEntitiesInRange<InventoryComponent>(Transform(uid).Coordinates, 1.5f))
                {
                    if (TryEquip(uid, target, faceHugger))
                        break;
                }
            }
        }
    }

    public bool TryEquip(EntityUid uid, EntityUid target, FaceHuggerComponent component)
    {
        if (!component.Active || _mobState.IsDead(uid) || HasComp<FaceHuggerImmuneComponent>(target) ||
            !_whitelist.CheckBoth(target, component.Blacklist))
            return false;

        if (TryGetBlocker(target, out var blocker))
        {
            if (TryComp<BreathToolComponent>(blocker, out _))
            {
                _damageable.TryChangeDamage(target, component.MaskBlockDamage);
                _audio.PlayPvs(component.MaskBlockSound, uid);
                _popup.PopupEntity(Loc.GetString("xenomorphs-face-hugger-mask-blocked", ("mask", blocker), ("facehugger", uid)), target, target);
                _popup.PopupEntity(Loc.GetString("xenomorphs-face-hugger-mask-blocked-other", ("facehugger", uid), ("target", target), ("mask", blocker)), target, Filter.PvsExcept(target), true);
                component.RestIn = _timing.CurTime + component.AttachAttemptDelay;
                component.Active = false;
                _transform.SetCoordinates(uid, Transform(target).Coordinates.Offset(_random.NextVector2(0.5f)));
                return false;
            }

            _audio.PlayPvs(component.SoundOnImpact, uid);
            _damageable.TryChangeDamage(uid, component.DamageOnImpact);
            _popup.PopupEntity(Loc.GetString("xenomorphs-face-hugger-try-equip", ("equipment", uid), ("equipmentBlocker", blocker)), uid);
            _popup.PopupEntity(Loc.GetString("xenomorphs-face-hugger-try-equip-other", ("equipment", uid), ("equipmentBlocker", blocker), ("target", Identity.Entity(target, EntityManager))), uid, Filter.PvsExcept(target), true);
            return false;
        }

        component.RestIn = _timing.CurTime + _random.Next(component.MinRestTime, component.MaxRestTime);
        component.Active = false;
        return _inventory.TryEquip(target, uid, component.Slot, true, true);
    }

    private bool TryGetBlocker(EntityUid target, out EntityUid blocker)
    {
        if (_inventory.TryGetSlotEntity(target, "head", out var head))
        {
            if (HasComp<FaceHuggerBlockerComponent>(head) &&
                (!TryComp<SealableClothingComponent>(head, out var sealable) || sealable.IsSealed))
            {
                blocker = head.Value;
                return true;
            }
            _inventory.TryUnequip(target, "head", true);
        }

        if (_inventory.TryGetSlotEntity(target, "mask", out var mask))
        {
            if (TryComp<IngestionBlockerComponent>(mask, out var ingestion) && ingestion.Enabled)
            {
                blocker = mask.Value;
                return true;
            }
            _inventory.TryUnequip(target, "mask", true);
        }

        blocker = default;
        return false;
    }

    private void Inject(FaceHuggerComponent component, EntityUid target)
    {
        if (!TryComp<BloodstreamComponent>(target, out var bloodstream) ||
            !_solutions.ResolveSolution(target, bloodstream.MetabolitesSolutionName, ref bloodstream.MetabolitesSolution, out var solution))
            return;

        var reagent = new ReagentId(component.SleepChem, null);
        if (solution.TryGetReagentQuantity(reagent, out var quantity) && quantity > FixedPoint2.New(component.MinChemicalThreshold))
            return;

        var injected = new Solution();
        injected.AddReagent(reagent, component.SleepChemAmount);
        if (_solutions.TryAddSolution(bloodstream.MetabolitesSolution.Value, injected))
            _reactive.DoEntityReaction(target, injected, ReactionMethod.Injection);
    }

    private void Infect(EntityUid uid, FaceHuggerComponent component)
    {
        if (component.InfectionPrototype is not { } prototype ||
            !TryComp<ClothingComponent>(uid, out var clothing) || clothing.InSlot != component.Slot ||
            !_container.TryGetContainingContainer(uid, out var container) ||
            HasComp<FaceHuggerImmuneComponent>(container.Owner))
            return;

        EntityUid? part = null;
        foreach (var candidate in _body.GetBodyChildrenOfType(container.Owner, component.InfectionBodyPart.Type))
        {
            if (candidate.Component.Symmetry == component.InfectionBodyPart.Symmetry)
            {
                part = candidate.Id;
                break;
            }
        }

        if (part == null)
            return;

        var infection = Spawn(prototype);
        if (!_body.TryInsertOrgan(part.Value, infection, component.InfectionSlotId, checkCompatibility: false))
        {
            QueueDel(infection);
            return;
        }

        _damageable.TryChangeDamage(container.Owner, component.DamageOnInfect, true);
    }
}
