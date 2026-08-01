using System.Linq;
using System.Numerics;
using Content.Goobstation.Shared.GrabIntent;
using Content.Shared.Actions;
using Content.Shared._Onyx.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing;
using Content.Shared.Changeling.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementModStatusSystem _movement = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;

    private static readonly EntProtoId SlowdownEffect = "MartialArtsGenericSlowdownEffect";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MartialArtsKnowledgeComponent, ComboAttackPerformedEvent>(OnAttack);
        SubscribeLocalEvent<MartialArtsKnowledgeComponent, ShotAttemptedEvent>(OnShotAttempt);
        SubscribeLocalEvent<MartialArtBlockedComponent, ShotAttemptedEvent>(OnBlockedShot);
        SubscribeLocalEvent<MartialArtBlockedComponent, StaminaDamageOnHitAttemptEvent>(OnBlockedStaminaHit);
        SubscribeLocalEvent<MartialArtBlockedComponent, ItemToggleActivateAttemptEvent>(OnBlockedToggle);
        SubscribeLocalEvent<MartialArtsKnowledgeComponent, ComponentShutdown>(OnKnowledgeShutdown);
        SubscribeLocalEvent<CanPerformComboComponent, GetPerformedAttackTypesEvent>(OnGetAttackTypes);
        SubscribeLocalEvent<KravMagaSilencedComponent, SpeakAttemptEvent>(OnSpeakAttempt);
        SubscribeLocalEvent<KravMagaComponent, ComponentInit>(OnKravInit);
        SubscribeLocalEvent<KravMagaComponent, ComponentShutdown>(OnKravShutdown);
        SubscribeLocalEvent<KravMagaComponent, KravMagaActionEvent>(OnKravAction);
        SubscribeLocalEvent<KravMagaComponent, MeleeHitEvent>(OnKravHit);
        SubscribeLocalEvent<GrantCqcComponent, UseInHandEvent>(OnUseCqc);
        SubscribeLocalEvent<GrantCqcComponent, MapInitEvent>(OnLegacyCqc);
        SubscribeLocalEvent<GrantCapoeiraComponent, UseInHandEvent>(OnUseCapoeira);
        SubscribeLocalEvent<GrantKungFuDragonComponent, UseInHandEvent>(OnUseDragon);
        SubscribeLocalEvent<GrantNinjutsuComponent, UseInHandEvent>(OnUseNinjutsu);
        SubscribeLocalEvent<GrantHellRipComponent, UseInHandEvent>(OnUseHellRip);
        SubscribeLocalEvent<GrantHellRipComponent, MapInitEvent>(OnLegacyHellRip);
        SubscribeLocalEvent<GrantSleepingCarpComponent, UseInHandEvent>(OnUseCarp);
        SubscribeLocalEvent<GrantCorporateJudoComponent, ClothingGotEquippedEvent>(OnJudoEquipped);
        SubscribeLocalEvent<GrantCorporateJudoComponent, ClothingGotUnequippedEvent>(OnJudoUnequipped);
        SubscribeLocalEvent<ArmbarredComponent, PullStoppedMessage>(OnArmbarStopped);
        SubscribeLocalEvent<MeleeHitEvent>(OnNinjutsuHit);
        SubscribeLocalEvent<InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<GetMeleeAttackRateEvent>(OnAttackRate);
        SubscribeLocalEvent<GetMeleeDamageEvent>(OnMeleeDamage);
        SubscribeLocalEvent<MartialArtModifiersComponent, RefreshMovementSpeedModifiersEvent>(OnMoveSpeed);
        SubscribeLocalEvent<DragonKungFuComponent, BeforeDamageChangedEvent>(OnDragonDamaged);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private bool IsMartialArtBlocked(EntityUid user, MartialArtBlockedComponent blocked)
    {
        return TryComp<MartialArtsKnowledgeComponent>(user, out var knowledge) &&
               knowledge.MartialArtsForm == blocked.Form;
    }

    private void OnBlockedShot(Entity<MartialArtBlockedComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!IsMartialArtBlocked(args.User, ent.Comp))
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("martial-arts-blocked-weapon"), args.User, args.User);
    }

    private void OnBlockedStaminaHit(Entity<MartialArtBlockedComponent> ent, ref StaminaDamageOnHitAttemptEvent args)
    {
        if (args.User is not { } user || !IsMartialArtBlocked(user, ent.Comp))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("martial-arts-blocked-weapon"), user, user);
    }

    private void OnBlockedToggle(Entity<MartialArtBlockedComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (args.User is not { } user || !IsMartialArtBlocked(user, ent.Comp))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("martial-arts-blocked-weapon"), user, user);
    }

    public override void Update(float frameTime)
    {
        var combos = EntityQueryEnumerator<CanPerformComboComponent>();
        while (!_net.IsClient && combos.MoveNext(out var uid, out var combo))
        {
            if (combo.LastAttacks.Count == 0 || _timing.CurTime < combo.ResetAt)
                continue;
            combo.LastAttacks.Clear();
            combo.ConsecutiveGnashes = 0;
            Dirty(uid, combo);
        }

        var silenced = EntityQueryEnumerator<KravMagaSilencedComponent>();
        while (silenced.MoveNext(out var uid, out var comp))
            if (_timing.CurTime >= comp.Until)
                RemCompDeferred<KravMagaSilencedComponent>(uid);

        var breathing = EntityQueryEnumerator<KravMagaBlockedBreathingComponent>();
        while (breathing.MoveNext(out var uid, out var comp))
            if (_timing.CurTime >= comp.Until)
                RemCompDeferred<KravMagaBlockedBreathingComponent>(uid);

        var modifiers = EntityQueryEnumerator<MartialArtModifiersComponent>();
        while (modifiers.MoveNext(out var uid, out var modifier))
        {
            if (_timing.CurTime >= modifier.AttackRateUntil) modifier.AttackRate = 1f;
            if (_timing.CurTime >= modifier.DamageUntil) modifier.Damage = 1f;
            if (_timing.CurTime >= modifier.MoveSpeedUntil && modifier.MoveSpeed != 1f)
            {
                modifier.MoveSpeed = 1f;
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }
            if (modifier.AttackRate == 1f && modifier.Damage == 1f && modifier.MoveSpeed == 1f)
                RemCompDeferred<MartialArtModifiersComponent>(uid);
        }

        var dragons = EntityQueryEnumerator<DragonKungFuComponent, PhysicsComponent>();
        while (dragons.MoveNext(out var uid, out var dragon, out var physics))
        {
            if (physics.LinearVelocity.Length() >= dragon.MinVelocity)
            {
                dragon.LastMoveTime = _timing.CurTime;
                dragon.PowerReady = false;
            }
            else if (_timing.CurTime >= dragon.LastMoveTime + dragon.PauseDuration)
                dragon.PowerReady = true;
        }
    }

    public bool TrySetForm(EntityUid uid, MartialArtsForms? form, bool blocked = false)
    {
        if (!Exists(uid))
            return false;
        if (form != null && HasComp<ChangelingIdentityComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("cqc-fail-changeling"), uid, uid);
            return false;
        }
        if (form is null)
        {
            RevokeFormEffects(uid);
            RemComp<MartialArtsKnowledgeComponent>(uid);
            RemComp<CanPerformComboComponent>(uid);
            RemComp<NinjutsuSneakAttackComponent>(uid);
            RemComp<DragonKungFuComponent>(uid);
            RemComp<MartialArtModifiersComponent>(uid);
            if (!HasComp<KravMagaComponent>(uid))
                RemComp<MartialArtsPolymorphComponent>(uid);
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
            return true;
        }
        if (HasComp<MartialArtsKnowledgeComponent>(uid) || HasComp<KravMagaComponent>(uid))
            return false;
        if (!_prototypes.TryIndex<MartialArtPrototype>(form.Value.ToString(), out var art))
            return false;
        var knowledge = EnsureComp<MartialArtsKnowledgeComponent>(uid);
        EnsureComp<MartialArtsPolymorphComponent>(uid);
        knowledge.MartialArtsForm = form.Value;
        knowledge.Blocked = blocked;
        knowledge.DamageBonus = art.BaseDamageModifier;
        EnsureComp<CanPerformComboComponent>(uid).LastAttacks.Clear();
        if (form == MartialArtsForms.Ninjutsu)
            EnsureComp<NinjutsuSneakAttackComponent>(uid);
        if (form == MartialArtsForms.KungFuDragon)
            EnsureComp<DragonKungFuComponent>(uid);
        if (form == MartialArtsForms.SleepingCarp)
            GrantSleepingCarpEffects(uid);
        Dirty(uid, knowledge);
        return true;
    }

    private void OnGetAttackTypes(Entity<CanPerformComboComponent> ent, ref GetPerformedAttackTypesEvent args)
        => args.AttackTypes = _timing.CurTime < ent.Comp.ResetAt ? ent.Comp.LastAttacks : null;

    public void GrantSleepingCarpEffects(EntityUid uid)
    {
        if (HasComp<SleepingCarpEffectsComponent>(uid))
            return;

        var effects = EnsureComp<SleepingCarpEffectsComponent>(uid);
        if (!TryComp<ReflectComponent>(uid, out var reflect))
        {
            reflect = AddComp<ReflectComponent>(uid);
            effects.AddedReflect = true;
        }
        else
        {
            effects.OriginalReflectProbability = reflect.ReflectProb;
            effects.OriginalReflectSpread = reflect.Spread;
        }

        reflect.ReflectProb = 1f;
        reflect.Spread = Angle.FromDegrees(60);
        Dirty(uid, reflect);

        const string dragon = "Dragon";
        if (!_faction.IsMember(uid, dragon))
        {
            _faction.AddFaction(uid, dragon);
            effects.AddedDragonFaction = true;
        }
    }

    private void RevokeFormEffects(EntityUid uid)
    {
        if (!TryComp<SleepingCarpEffectsComponent>(uid, out var effects))
            return;

        if (effects.AddedReflect)
            RemComp<ReflectComponent>(uid);
        else if (TryComp<ReflectComponent>(uid, out var reflect))
        {
            reflect.ReflectProb = effects.OriginalReflectProbability;
            reflect.Spread = effects.OriginalReflectSpread;
            Dirty(uid, reflect);
        }

        if (effects.AddedDragonFaction)
        {
            const string dragon = "Dragon";
            _faction.RemoveFaction(uid, dragon);
        }

        RemComp<SleepingCarpEffectsComponent>(uid);
    }

    private void OnUseCqc(Entity<GrantCqcComponent> ent, ref UseInHandEvent args) => UseManual(ent, ent.Comp, ref args);
    private void OnUseCapoeira(Entity<GrantCapoeiraComponent> ent, ref UseInHandEvent args) => UseManual(ent, ent.Comp, ref args);
    private void OnUseDragon(Entity<GrantKungFuDragonComponent> ent, ref UseInHandEvent args) => UseManual(ent, ent.Comp, ref args);
    private void OnUseNinjutsu(Entity<GrantNinjutsuComponent> ent, ref UseInHandEvent args) => UseManual(ent, ent.Comp, ref args);
    private void OnUseHellRip(Entity<GrantHellRipComponent> ent, ref UseInHandEvent args) => UseManual(ent, ent.Comp, ref args);

    private void UseManual(EntityUid manual, GrantMartialArtKnowledgeComponent comp, ref UseInHandEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;
        args.Handled = true;
        if (comp.MartialArtsForm == MartialArtsForms.CloseQuartersCombat
            && TryComp<MartialArtsKnowledgeComponent>(args.User, out var known)
            && known.MartialArtsForm == MartialArtsForms.CloseQuartersCombat
            && known.Blocked)
        {
            known.Blocked = false;
            Dirty(args.User, known);
            _popup.PopupEntity(Loc.GetString("cqc-success-unblocked"), args.User, args.User);
        }
        else if (!TrySetForm(args.User, comp.MartialArtsForm))
        {
            _popup.PopupEntity(Loc.GetString("cqc-fail-knowanother"), args.User, args.User);
            return;
        }
        if (comp.LearnMessage is { } message)
            _popup.PopupEntity(Loc.GetString(message), args.User, args.User);
        _audio.PlayPvs(comp.SoundOnUse, args.User);
        if (comp.MultiUse)
            return;
        var coordinates = Transform(args.User).Coordinates;
        QueueDel(manual);
        if (comp.SpawnedProto is { } spawned)
            Spawn(spawned, coordinates);
    }

    private void OnLegacyCqc(Entity<GrantCqcComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<MobStateComponent>(ent))
            TrySetForm(ent, MartialArtsForms.CloseQuartersCombat, ent.Comp.IsBlocked);
    }

    private void OnUseCarp(Entity<GrantSleepingCarpComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;
        args.Handled = true;
        if (ent.Comp.CurrentUses >= ent.Comp.MaximumUses)
        {
            _popup.PopupEntity(Loc.GetString("cqc-fail-used", ("manual", Name(ent))), args.User, args.User);
            return;
        }
        var student = EnsureComp<SleepingCarpStudentComponent>(args.User);
        if (student.UseAgainTime != TimeSpan.Zero && _timing.CurTime < student.UseAgainTime)
        {
            _popup.PopupEntity(Loc.GetString("carp-scroll-waiting"), args.User, args.User);
            return;
        }
        if (student.Stage < 3)
        {
            student.Stage++;
            student.UseAgainTime = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(student.MinUseDelay, student.MaxUseDelay));
            _popup.PopupEntity(Loc.GetString("carp-scroll-advance"), args.User, args.User);
            return;
        }
        if (!TrySetForm(args.User, MartialArtsForms.SleepingCarp))
            return;
        ent.Comp.CurrentUses++;
        _popup.PopupEntity(Loc.GetString("carp-scroll-complete"), args.User, args.User, PopupType.LargeCaution);
    }

    private void OnJudoEquipped(Entity<GrantCorporateJudoComponent> ent, ref ClothingGotEquippedEvent args)
    {
        var sources = EnsureComp<CorporateJudoGrantSourcesComponent>(args.Wearer);
        sources.Count++;
        if (sources.Count == 1)
            sources.GrantedArt = TrySetForm(args.Wearer, MartialArtsForms.CorporateJudo);
    }

    private void OnJudoUnequipped(Entity<GrantCorporateJudoComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (!TryComp<CorporateJudoGrantSourcesComponent>(args.Wearer, out var sources))
            return;

        sources.Count = Math.Max(0, sources.Count - 1);
        if (sources.Count == 0
            && sources.GrantedArt
            && TryComp<MartialArtsKnowledgeComponent>(args.Wearer, out var art)
            && art.MartialArtsForm == MartialArtsForms.CorporateJudo)
            TrySetForm(args.Wearer, null);
        if (sources.Count == 0)
            RemComp<CorporateJudoGrantSourcesComponent>(args.Wearer);
    }

    private void OnKnowledgeShutdown(Entity<MartialArtsKnowledgeComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        RevokeFormEffects(ent);
        RemComp<MartialArtModifiersComponent>(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnLegacyHellRip(Entity<GrantHellRipComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<MobStateComponent>(ent))
            TrySetForm(ent, MartialArtsForms.HellRip);
    }

    private void OnShotAttempt(Entity<MartialArtsKnowledgeComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.MartialArtsForm != MartialArtsForms.SleepingCarp)
            return;
        _popup.PopupEntity(Loc.GetString("gun-disabled"), ent, ent);
        args.Cancel();
    }

    private void OnAttack(Entity<MartialArtsKnowledgeComponent> ent, ref ComboAttackPerformedEvent args)
    {
        if (ent.Comp.Blocked || args.Weapon != ent.Owner || !HasComp<MobStateComponent>(args.Target))
            return;
        if (!TryComp<CanPerformComboComponent>(ent, out var combo))
            return;
        if (combo.CurrentTarget != args.Target || _timing.CurTime > combo.ResetAt)
        {
            combo.LastAttacks.Clear();
            combo.ConsecutiveGnashes = 0;
        }
        combo.CurrentTarget = args.Target;
        combo.ResetAt = _timing.CurTime + combo.ResetAfter;
        combo.LastAttacks.Add(args.Type);
        if (combo.LastAttacks.Count > 4)
            combo.LastAttacks.RemoveAt(0);
        ApplyPassive(ent, args);
        if (!_prototypes.TryIndex<MartialArtPrototype>(ent.Comp.MartialArtsForm.ToString(), out var art)
            || !_prototypes.TryIndex(art.RoundstartCombos, out var list))
            return;
        foreach (var id in list.Combos)
        {
            var move = _prototypes.Index(id);
            if (move.AttackTypes.Count > combo.LastAttacks.Count
                || !combo.LastAttacks.TakeLast(move.AttackTypes.Count).SequenceEqual(move.AttackTypes)
                || !move.CanDoWhileProne && _standing.IsDown(ent.Owner)
                || ent.Owner == args.Target != move.PerformOnSelf)
                continue;
            if (PerformMove(ent, move.PerformOnSelf ? ent.Owner : args.Target, move, combo))
                combo.LastAttacks.Clear();
            break;
        }
        Dirty(ent.Owner, combo);
    }

    private void ApplyPassive(Entity<MartialArtsKnowledgeComponent> ent, ComboAttackPerformedEvent args)
    {
        if (args.Type == ComboAttackType.Disarm && ent.Comp.MartialArtsForm == MartialArtsForms.CloseQuartersCombat)
            _stamina.TakeStaminaDamage(args.Target, 25f, source: ent);
        if (ent.Comp.MartialArtsForm == MartialArtsForms.CloseQuartersCombat
            && args.Type == ComboAttackType.Harm
            && _standing.IsDown(ent.Owner)
            && !_standing.IsDown(args.Target))
        {
            _standing.Stand(ent.Owner);
            _stun.TryKnockdown(args.Target, TimeSpan.FromSeconds(5), force: true);
            ComboPopup(ent, args.Target, "LegSweep");
        }
        if (ent.Comp.MartialArtsForm == MartialArtsForms.CloseQuartersCombat
            && args.Type == ComboAttackType.Harm
            && TryComp<PullerComponent>(ent, out var puller)
            && puller.Pulling == args.Target
            && TryComp<GrabIntentComponent>(ent, out var grab)
            && grab.GrabStage == GrabStage.Suffocate
            && TryComp<StaminaComponent>(args.Target, out var stamina)
            && stamina.Critical
            && TryComp<TargetingComponent>(ent, out var targeting)
            && targeting.Target == TargetBodyPart.Head)
        {
            Damage(args.Target, ent, "Blunt", 300);
            ComboPopup(ent, args.Target, "NeckSnap");
        }
        if (ent.Comp.MartialArtsForm == MartialArtsForms.Capoeira)
        {
            var velocity = TryComp<PhysicsComponent>(ent, out var physics) ? physics.LinearVelocity.Length() : 0f;
            var modifier = EnsureComp<MartialArtModifiersComponent>(ent);
            modifier.AttackRate = Math.Clamp(MathF.Pow(velocity, 0.2f), 1f, 1.5f);
            modifier.AttackRateUntil = _timing.CurTime + TimeSpan.FromSeconds(3);
            if (args.Type == ComboAttackType.Grab)
            {
                modifier.MoveSpeed = 1.2f;
                modifier.MoveSpeedUntil = _timing.CurTime + TimeSpan.FromSeconds(4);
                _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
            }
        }
        if (ent.Comp.DamageBonus <= 0f || args.Type is not (ComboAttackType.Harm or ComboAttackType.HarmLight))
            return;
        var bonus = ent.Comp.DamageBonus;
        var art = _prototypes.Index<MartialArtPrototype>(ent.Comp.MartialArtsForm.ToString());
        if (art.RandomDamageModifier)
            bonus += _random.Next(art.MinRandomDamageModifier, art.MaxRandomDamageModifier + 1);
        if (TryComp<MartialArtModifiersComponent>(ent, out var modifiers))
            bonus *= modifiers.Damage;
        Damage(args.Target, ent, _prototypes.Index<MartialArtPrototype>(ent.Comp.MartialArtsForm.ToString()).DamageModifierType, bonus);
    }

    private bool PerformMove(EntityUid performer, EntityUid target, ComboPrototype move, CanPerformComboComponent combo)
    {
        var downed = _standing.IsDown(target);
        var velocity = TryComp<PhysicsComponent>(performer, out var physics) ? physics.LinearVelocity.Length() : 0f;
        if (move.MinVelocity > velocity)
        {
            _popup.PopupEntity(Loc.GetString("capoeira-fail-low-velocity"), performer, performer);
            return false;
        }
        var power = move.MartialArtsForm == MartialArtsForms.Capoeira ? Math.Clamp(velocity * 0.6f, 1f, 3f) : 1f;
        switch (move.Effect)
        {
            case MartialArtEffect.JudoDiscombobulate:
            case MartialArtEffect.DragonClaw:
                _movement.TryUpdateMovementSpeedModDuration(target, SlowdownEffect, TimeSpan.FromSeconds(5), 0.5f);
                break;
            case MartialArtEffect.JudoEyePoke:
                _blindable.AdjustEyeDamage(target, 7);
                break;
            case MartialArtEffect.JudoThrow:
                if (downed) return false;
                Knockdown(target, move);
                StopPull(target, performer);
                break;
            case MartialArtEffect.JudoArmbar:
                if (!downed || !TryComp<PullerComponent>(performer, out var puller) || puller.Pulling != target) return false;
                EnsureComp<ArmbarredComponent>(target).Puller = performer;
                if (TryComp<GrabIntentComponent>(performer, out var grabber)
                    && TryComp<GrabbableComponent>(target, out var grabbable))
                {
                    grabber.GrabStage = GrabStage.Suffocate;
                    grabbable.GrabStage = GrabStage.Suffocate;
                    Dirty(performer, grabber);
                    Dirty(target, grabbable);
                }
                Knockdown(target, move);
                break;
            case MartialArtEffect.JudoWheelThrow:
                if (!downed || !TryComp<ArmbarredComponent>(target, out var armbar) || armbar.Puller != performer) return false;
                StopPull(target, performer);
                ThrowAway(performer, target, 5f);
                break;
            case MartialArtEffect.CqcSlam:
            case MartialArtEffect.HellRipSlam:
                if (downed) return false;
                Knockdown(target, move);
                StopPull(target, performer);
                if (move.Effect == MartialArtEffect.HellRipSlam)
                    _standing.Stand(performer);
                break;
            case MartialArtEffect.CqcKick:
                if (downed)
                {
                    Damage(target, performer, move.DamageType, move.ExtraDamage);
                    _stamina.TakeStaminaDamage(target, move.StaminaDamage + 5, source: performer);
                }
                StopPull(target, performer);
                ThrowAway(performer, target, move.ThrownSpeed);
                break;
            case MartialArtEffect.CqcRestrain:
                Knockdown(target, move);
                break;
            case MartialArtEffect.CqcPressure:
                StealActiveItem(performer, target);
                break;
            case MartialArtEffect.CarpGnashingTeeth:
                Damage(target, performer, move.DamageType, move.ExtraDamage + combo.ConsecutiveGnashes++ * 5);
                if (_prototypes.TryIndex<MartialArtPrototype>(MartialArtsForms.SleepingCarp.ToString(), out var carp))
                {
                    var sayings = downed ? carp.RandomSayingsDowned : carp.RandomSayings;
                    if (sayings.Count > 0)
                        RaiseLocalEvent(performer, new SleepingCarpSaying(_random.Pick(sayings)));
                }
                break;
            case MartialArtEffect.CarpKneeHaul:
                if (!downed) Knockdown(target, move); else _hands.TryDrop(target);
                StopPull(target, performer);
                break;
            case MartialArtEffect.CarpCrashingWaves:
            case MartialArtEffect.PushKick:
                if (downed) return false;
                Knockdown(target, move, power);
                StopPull(target, performer);
                ThrowAway(performer, target, move.ThrownSpeed * power);
                break;
            case MartialArtEffect.CircleKick:
                _movement.TryUpdateMovementSpeedModDuration(target, SlowdownEffect, TimeSpan.FromSeconds(5 * power), 1f / power);
                break;
            case MartialArtEffect.SweepKick:
            case MartialArtEffect.SpinKick:
                if (downed && move.Effect == MartialArtEffect.SpinKick) return false;
                Knockdown(target, move, power);
                break;
            case MartialArtEffect.KickUp:
                _standing.Stand(performer);
                break;
            case MartialArtEffect.DragonTail:
                if (downed) _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(2)); else Knockdown(target, move);
                StopPull(target, performer);
                break;
            case MartialArtEffect.DragonStrike:
            case MartialArtEffect.DirtyKill:
                if (!downed) return false;
                _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(move.ParalyzeTime));
                break;
            case MartialArtEffect.BiteTheDust:
                if (downed) return false;
                Knockdown(target, move);
                break;
            case MartialArtEffect.HellRipDropKick:
                if (!downed) return false;
                StopPull(target, performer);
                ThrowAway(performer, target, 25f);
                break;
            case MartialArtEffect.HellRipHeadRip:
                if (!_mobState.IsDead(target)) return false;
                Damage(target, performer, "Blunt", 300);
                StopPull(target, performer);
                var head = _body.GetBodyChildrenOfType(target, BodyPartType.Head).FirstOrDefault().Id;
                if (head != default)
                    _body.TryDetachPart(head);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Onyx/Weapons/Effects/guillotine.ogg"), target);
                break;
            case MartialArtEffect.HellRipTearDown:
                StopPull(target, performer);
                _bloodstream.TryModifyBleedAmount(target, 5f);
                break;
        }
        if (move.ExtraDamage > 0 && move.Effect is not (MartialArtEffect.CarpGnashingTeeth or MartialArtEffect.CqcKick))
            Damage(target, performer, move.DamageType, move.ExtraDamage * power);
        if (move.StaminaDamage != 0)
            _stamina.TakeStaminaDamage(target, move.StaminaDamage, source: performer);
        if (move.StaminaToHeal != 0)
            _stamina.TryTakeStamina(performer, move.StaminaToHeal, source: performer);
        if (move.AttackSpeedMultiplierTime > 0f && move.AttackSpeedMultiplier != 1f)
        {
            var modifier = EnsureComp<MartialArtModifiersComponent>(performer);
            modifier.AttackRate = move.AttackSpeedMultiplier;
            modifier.AttackRateUntil = _timing.CurTime + TimeSpan.FromSeconds(move.AttackSpeedMultiplierTime);
        }
        ComboPopup(performer, target, move.ID);
        return true;
    }

    private void Knockdown(EntityUid target, ComboPrototype move, float multiplier = 1f)
        => _stun.TryKnockdown(target, TimeSpan.FromSeconds(move.ParalyzeTime * multiplier), drop: move.DropItems, force: true);

    private void Damage(EntityUid target, EntityUid performer, string type, float amount)
    {
        if (amount <= 0 || !_prototypes.TryIndex<DamageTypePrototype>(type, out var damageType))
            return;
        _damage.TryChangeDamage(target, new DamageSpecifier(damageType, amount), origin: performer);
    }

    private void StopPull(EntityUid target, EntityUid user)
    {
        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, user);
    }

    private void ThrowAway(EntityUid performer, EntityUid target, float speed)
    {
        var direction = _transform.GetMapCoordinates(target).Position - _transform.GetMapCoordinates(performer).Position;
        _throwing.TryThrow(target, direction, speed, performer);
    }

    private void StealActiveItem(EntityUid performer, EntityUid target)
    {
        if (!_hands.TryGetActiveItem(target, out var item) || !_hands.TryDrop(target, item.Value))
            return;
        _hands.TryPickupAnyHand(performer, item.Value);
    }

    private void ComboPopup(EntityUid performer, EntityUid target, string move)
    {
        if (_net.IsClient)
            return;
        var key = "martial-arts-combo-" + move;
        var name = Loc.TryGetString(key, out var localized) ? localized : move;
        _popup.PopupEntity(Loc.GetString("martial-arts-action-sender", ("name", Identity.Entity(target, EntityManager)), ("move", name)), performer, performer);
        _popup.PopupEntity(Loc.GetString("martial-arts-action-receiver", ("name", Identity.Entity(performer, EntityManager)), ("move", name)), target, target);
    }

    private void OnArmbarStopped(Entity<ArmbarredComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PullerUid == ent.Comp.Puller)
            RemCompDeferred<ArmbarredComponent>(ent);
    }

    private void OnNinjutsuHit(MeleeHitEvent args)
    {
        if (args.HitEntities.Count == 0
            && TryComp<MartialArtsKnowledgeComponent>(args.User, out var art)
            && art.MartialArtsForm == MartialArtsForms.Capoeira
            && args.Weapon == args.User)
        {
            var modifier = EnsureComp<MartialArtModifiersComponent>(args.User);
            modifier.Damage = 2f;
            modifier.DamageUntil = _timing.CurTime + TimeSpan.FromSeconds(3);
            if (TryComp<MeleeWeaponComponent>(args.Weapon, out var melee))
            {
                melee.NextAttack -= TimeSpan.FromSeconds(0.5);
                Dirty(args.Weapon, melee);
            }
        }
        if (!args.IsHit || args.HitEntities.Count == 0
            || !TryComp<NinjutsuSneakAttackComponent>(args.User, out var sneak)
            || _timing.CurTime < sneak.SurpriseReadyAt)
            return;
        var target = args.HitEntities[0];
        if (target == args.User || args.Weapon != args.User && !args.BaseDamage.DamageDict.ContainsKey("Slash"))
            return;
        args.BonusDamage = args.BaseDamage * (sneak.Multiplier - 1f);
        if (args.Direction == null)
            Damage(target, args.User, args.Weapon == args.User ? "Blunt" : "Slash", args.Weapon == args.User ? 15 : 25);
        sneak.SurpriseReadyAt = _timing.CurTime + TimeSpan.FromSeconds(5);
    }

    private void OnInteractHand(InteractHandEvent args)
    {
        if (args.User == args.Target || !TryComp<NinjutsuSneakAttackComponent>(args.User, out var sneak))
            return;
        if (_timing.CurTime < sneak.SurpriseReadyAt || _standing.IsDown(args.Target))
            return;
        _movement.TryUpdateMovementSpeedModDuration(args.Target, SlowdownEffect, TimeSpan.FromSeconds(4), 0.5f);
        EnsureComp<KravMagaSilencedComponent>(args.Target).Until = _timing.CurTime + TimeSpan.FromSeconds(4);
        sneak.SurpriseReadyAt = _timing.CurTime + TimeSpan.FromSeconds(5);
        ComboPopup(args.User, args.Target, "Ninjutsu-Takedown");
    }

    private void OnAttackRate(ref GetMeleeAttackRateEvent args)
    {
        if (TryComp<MartialArtModifiersComponent>(args.User, out var modifier))
            args.Multipliers *= modifier.AttackRate;
    }

    private void OnMeleeDamage(ref GetMeleeDamageEvent args)
    {
        if (TryComp<MartialArtModifiersComponent>(args.User, out var modifier))
            args.Damage *= modifier.Damage;
    }

    private void OnMoveSpeed(Entity<MartialArtModifiersComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
        => args.ModifySpeed(ent.Comp.MoveSpeed, ent.Comp.MoveSpeed);

    private void OnDragonDamaged(Entity<DragonKungFuComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!ent.Comp.PowerReady || _hands.TryGetActiveItem(ent.Owner, out _))
            return;
        args.Damage *= 0.5f;
        ent.Comp.PowerReady = false;
        ent.Comp.LastMoveTime = _timing.CurTime;
        var modifier = EnsureComp<MartialArtModifiersComponent>(ent);
        modifier.Damage = 1.5f;
        modifier.DamageUntil = _timing.CurTime + TimeSpan.FromSeconds(5);
    }

    private void OnKravInit(Entity<KravMagaComponent> ent, ref ComponentInit args)
    {
        if (HasComp<MartialArtsKnowledgeComponent>(ent) || HasComp<ChangelingIdentityComponent>(ent))
        {
            RemCompDeferred<KravMagaComponent>(ent);
            return;
        }
        ent.Comp.Enabled = true;
        EnsureComp<MartialArtsPolymorphComponent>(ent);
        foreach (var id in new EntProtoId[] { "ActionLegSweep", "ActionNeckChop", "ActionLungPunch" })
            if (_actions.AddAction(ent, id) is { } action)
                ent.Comp.Actions.Add(action);
    }

    private void OnKravShutdown(Entity<KravMagaComponent> ent, ref ComponentShutdown args)
    {
        foreach (var action in ent.Comp.Actions)
            _actions.RemoveAction(action);
        if (!HasComp<MartialArtsKnowledgeComponent>(ent))
            RemComp<MartialArtsPolymorphComponent>(ent);
    }

    private void OnKravAction(Entity<KravMagaComponent> ent, ref KravMagaActionEvent args)
    {
        if (!TryComp<KravMagaActionComponent>(args.Action, out var action))
            return;
        args.Handled = true;
        ent.Comp.SelectedMove = action.Configuration;
        ent.Comp.SelectedStaminaDamage = action.StaminaDamage;
        ent.Comp.SelectedEffectTime = action.EffectTime;
        _popup.PopupEntity(Loc.GetString("krav-maga-ready",
            ("action", Loc.GetString($"krav-maga-move-{action.Configuration.ToString().ToLowerInvariant()}"))), ent, ent);
    }

    private void OnKravHit(Entity<KravMagaComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.Enabled || !args.IsHit || args.Weapon != ent.Owner || args.HitEntities.Count == 0)
            return;
        foreach (var target in args.HitEntities.Where(target => HasComp<MobStateComponent>(target)))
        {
            switch (ent.Comp.SelectedMove)
            {
                case KravMagaMoves.LegSweep:
                    if (!_standing.IsDown(target))
                        _stun.TryKnockdown(target, TimeSpan.FromSeconds(ent.Comp.SelectedEffectTime), force: true);
                    break;
                case KravMagaMoves.NeckChop:
                    EnsureComp<KravMagaSilencedComponent>(target).Until = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SelectedEffectTime);
                    break;
                case KravMagaMoves.LungPunch:
                    _stamina.TakeStaminaDamage(target, ent.Comp.SelectedStaminaDamage, source: ent);
                    EnsureComp<KravMagaBlockedBreathingComponent>(target).Until = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SelectedEffectTime);
                    break;
                case null:
                    Damage(target, ent, "Blunt", ent.Comp.BaseDamage * (_standing.IsDown(target) ? ent.Comp.DownedDamageModifier : 1));
                    break;
            }
        }
        ent.Comp.SelectedMove = null;
        ent.Comp.SelectedStaminaDamage = 0f;
        ent.Comp.SelectedEffectTime = 0f;
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead
            || args.OldMobState == MobState.Dead
            || args.Origin is not { } origin
            || !TryComp<MartialArtsKnowledgeComponent>(origin, out var art)
            || art.MartialArtsForm != MartialArtsForms.Ninjutsu)
            return;

        var modifier = EnsureComp<MartialArtModifiersComponent>(origin);
        modifier.MoveSpeed = 1.2f;
        modifier.MoveSpeedUntil = _timing.CurTime + TimeSpan.FromSeconds(3);
        _movementSpeed.RefreshMovementSpeedModifiers(origin);
    }

    private void OnSpeakAttempt(Entity<KravMagaSilencedComponent> ent, ref SpeakAttemptEvent args)
    {
        if (_timing.CurTime < ent.Comp.Until)
            args.Cancel();
    }
}
