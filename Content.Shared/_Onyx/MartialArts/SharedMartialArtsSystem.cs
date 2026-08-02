using System.Linq;
using System.Numerics;
using Content.Goobstation.Shared.GrabIntent;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared._Onyx.Sprinting;
using Content.Shared._Onyx.Targeting;
using Content.Shared.Bed.Sleep;
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
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
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

namespace Content.Shared._Onyx.MartialArts;

public abstract partial class SharedMartialArtsSystem : EntitySystem
{
    private delegate MoveResult MoveHandler(MoveContext context);

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BlurryVisionSystem _blurryVision = default!;
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
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedSprintingSystem _sprinting = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;

    private static readonly EntProtoId SlowdownEffect = "MartialArtsGenericSlowdownEffect";
    private static readonly ProtoId<AlertPrototype> DragonPowerAlert = "DragonPower";
    private static readonly ProtoId<AlertPrototype> SneakAttackAlert = "SneakAttack";
    private static readonly ProtoId<AlertPrototype> LossOfSurpriseAlert = "LossOfSurprise";
    private static readonly ProtoId<AlertCategoryPrototype> NinjutsuAlertCategory = "Ninjutsu";
    private static readonly DamageModifierSet DragonPowerResistance = new()
    {
        Coefficients =
        {
            { "Blunt", 0.6f },
            { "Slash", 0.6f },
            { "Piercing", 0.6f },
            { "Heat", 0.6f },
        },
    };

    public override void Initialize()
    {
        base.Initialize();
        InitializeMoveEvents();
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
        SubscribeLocalEvent<ArmbarredComponent, StoodEvent>(OnArmbarStood);
        SubscribeLocalEvent<MeleeHitEvent>(OnNinjutsuHit);
        SubscribeLocalEvent<NinjutsuSneakAttackComponent, ComponentStartup>(OnNinjutsuStartup);
        SubscribeLocalEvent<NinjutsuSneakAttackComponent, ComponentRemove>(OnNinjutsuRemove);
        SubscribeLocalEvent<NinjutsuSneakAttackComponent, SelfBeforeGunShotEvent>(OnNinjutsuGunshot);
        SubscribeLocalEvent<MartialArtsBlurryVisionStatusEffectComponent, StatusEffectRelayedEvent<GetBlurEvent>>(OnGetBlur);
        SubscribeLocalEvent<MartialArtsBlurryVisionStatusEffectComponent, StatusEffectAppliedEvent>(OnBlurChanged);
        SubscribeLocalEvent<MartialArtsBlurryVisionStatusEffectComponent, StatusEffectRemovedEvent>(OnBlurChanged);
        SubscribeLocalEvent<MeleeVulnerabilityStatusEffectComponent, StatusEffectRelayedEvent<GetMeleeTargetModifiersEvent>>(OnMeleeVulnerability);
        SubscribeLocalEvent<ThrownEvent>(OnThrown);
        SubscribeLocalEvent<InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<GetMeleeAttackRateEvent>(OnAttackRate);
        SubscribeLocalEvent<GetMeleeDamageEvent>(OnMeleeDamage);
        SubscribeLocalEvent<MartialArtModifiersComponent, RefreshMovementSpeedModifiersEvent>(OnMoveSpeed);
        SubscribeLocalEvent<DragonKungFuComponent, GetMeleeTargetModifiersEvent>(OnDragonMeleeModifiers);
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
            combo.CurrentTarget = null;
            combo.BeingPerformed = null;
            combo.MoveTarget = null;
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

        var ninjas = EntityQueryEnumerator<NinjutsuSneakAttackComponent>();
        while (ninjas.MoveNext(out var uid, out var ninja))
        {
            if (ninja.SurpriseReadyAt == TimeSpan.Zero || _timing.CurTime < ninja.SurpriseReadyAt)
                continue;
            ninja.SurpriseReadyAt = TimeSpan.Zero;
            _alerts.ShowAlert(uid, SneakAttackAlert);
        }

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

        if (_net.IsClient)
            return;

        var dragons = EntityQueryEnumerator<DragonKungFuComponent, PhysicsComponent, MobStateComponent>();
        while (dragons.MoveNext(out var uid, out var dragon, out var physics, out var mobState))
        {
            if (dragon.AlertShown && _timing.CurTime >= dragon.BuffUntil)
            {
                _alerts.ClearAlert(uid, DragonPowerAlert);
                dragon.AlertShown = false;
            }

            if (mobState.CurrentState != MobState.Alive)
                continue;
            if (physics.LinearVelocity.LengthSquared() > dragon.MinVelocitySquared)
            {
                dragon.LastMoveTime = _timing.CurTime;
                continue;
            }
            if (!_actionBlocker.CanInteract(uid, null)
                || _timing.CurTime < dragon.LastMoveTime + dragon.PauseDuration)
                continue;

            dragon.BuffUntil = _timing.CurTime + dragon.BuffLength;
            dragon.LastMoveTime = _timing.CurTime;
            if (!dragon.AlertShown)
            {
                _alerts.ShowAlert(uid, DragonPowerAlert);
                dragon.AlertShown = true;
            }
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
            _alerts.ClearAlert(uid, DragonPowerAlert);
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
        _alerts.ClearAlert(ent.Owner, DragonPowerAlert);
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
            if (combo.CurrentTarget != args.Target || _timing.CurTime >= combo.ResetAt)
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
            if (move.ResultEvent == null)
                continue;
            combo.BeingPerformed = move.ID;
            combo.MoveTarget = move.PerformOnSelf ? ent.Owner : args.Target;
            RaiseLocalEvent(ent.Owner, move.ResultEvent);
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
            if (args.Type == ComboAttackType.Grab)
            {
                modifier.MoveSpeed = 1.2f;
                modifier.MoveSpeedUntil = _timing.CurTime + TimeSpan.FromSeconds(4);
                _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
                if (TryComp<SprinterComponent>(args.Target, out var sprinter))
                {
                    _sprinting.ToggleSprint(args.Target, sprinter, false);
                    sprinter.LastSprint = _timing.CurTime + TimeSpan.FromSeconds(2);
                    Dirty(args.Target, sprinter);
                }
                return;
            }
            if (args.Type is ComboAttackType.Disarm or ComboAttackType.Harm)
            {
                modifier.AttackRate = Math.Clamp(MathF.Pow(velocity, 0.2f), 1f, 1.5f);
                modifier.AttackRateUntil = _timing.CurTime + TimeSpan.FromSeconds(3);
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

    private bool PerformMove(Entity<CanPerformComboComponent> ent, MoveHandler handler)
    {
        var moveId = ent.Comp.BeingPerformed;
        var target = ent.Comp.MoveTarget;
        ent.Comp.BeingPerformed = null;
        ent.Comp.MoveTarget = null;
        if (moveId == null
            || target == null
            || !Exists(target.Value)
            || !_prototypes.TryIndex(moveId.Value, out var move)
            || !TryComp<MartialArtsKnowledgeComponent>(ent, out var knowledge)
            || knowledge.Blocked
            || knowledge.MartialArtsForm != move.MartialArtsForm)
            return false;
        var performer = ent.Owner;
        var downed = _standing.IsDown(target.Value);
        var velocity = TryComp<PhysicsComponent>(performer, out var physics) ? physics.LinearVelocity.Length() : 0f;
        if (move.MinVelocity > velocity)
        {
            _popup.PopupEntity(Loc.GetString("capoeira-fail-low-velocity"), performer, performer);
            return false;
        }
        var power = move.MartialArtsForm == MartialArtsForms.Capoeira ? Math.Clamp(velocity * 0.6f, 1f, 4f) : 1f;
        var context = new MoveContext(performer, target.Value, move, ent.Comp, downed, power);
        var result = handler(context);
        if (!result.Success)
            return false;
        if (result.ApplyDamage && move.ExtraDamage > 0)
            Damage(target.Value, performer, move.DamageType, move.ExtraDamage * power);
        if (result.ApplyStamina && move.StaminaDamage != 0)
            _stamina.TakeStaminaDamage(target.Value, move.StaminaDamage, source: performer);
        if (move.StaminaToHeal != 0)
            _stamina.TryTakeStamina(performer, move.StaminaToHeal, source: performer);
        if (move.AttackSpeedMultiplierTime > 0f && move.AttackSpeedMultiplier != 1f)
        {
            var modifier = EnsureComp<MartialArtModifiersComponent>(performer);
            modifier.AttackRate = move.AttackSpeedMultiplier;
            modifier.AttackRateUntil = _timing.CurTime + TimeSpan.FromSeconds(move.AttackSpeedMultiplierTime);
        }
        if (result.ApplySound)
            _audio.PlayPvs(move.Sound, target.Value);
        if (result.ApplyPopup)
            ComboPopup(performer, target.Value, move.ID);
        ent.Comp.LastAttacks.Clear();
        ent.Comp.CurrentTarget = null;
        Dirty(ent);
        return true;
    }

    private void Knockdown(EntityUid target, ComboPrototype move, float multiplier = 1f)
        => _stun.TryKnockdown(target, TimeSpan.FromSeconds(move.ParalyzeTime * multiplier), drop: move.DropItems, force: true);

    private float GetStaminaResistance(EntityUid target)
    {
        var ev = new BeforeStaminaDamageEvent(1f);
        RaiseLocalEvent(target, ref ev);
        return ev.Cancelled ? 0f : ev.Value;
    }

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
        if (args.PullerUid != ent.Comp.Puller)
            return;
        RemCompDeferred<ArmbarredComponent>(ent);
    }

    private void OnArmbarStood(Entity<ArmbarredComponent> ent, ref StoodEvent args)
    {
        StopPull(ent, ent.Comp.Puller);
        RemCompDeferred<ArmbarredComponent>(ent);
    }

    private void OnGetBlur(Entity<MartialArtsBlurryVisionStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<GetBlurEvent> args)
    {
        var ev = args.Args;
        ev.Blur += BlurryVisionComponent.MaxMagnitude;
        args.Args = ev;
    }

    private void OnBlurChanged(Entity<MartialArtsBlurryVisionStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
        => _blurryVision.UpdateBlurMagnitude(args.Target);

    private void OnBlurChanged(Entity<MartialArtsBlurryVisionStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
        => _blurryVision.UpdateBlurMagnitude(args.Target);

    private void OnMeleeVulnerability(Entity<MeleeVulnerabilityStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<GetMeleeTargetModifiersEvent> args)
        => args.Args.Modifiers.Add(ent.Comp.Modifiers);

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
            modifier.DamageUnarmedOnly = true;
            if (TryComp<MeleeWeaponComponent>(args.Weapon, out var melee))
            {
                melee.NextAttack -= TimeSpan.FromSeconds(0.75f / _melee.GetAttackRate(args.Weapon, args.User, melee));
                Dirty(args.Weapon, melee);
            }
        }
        if (!args.IsHit || !TryComp<NinjutsuSneakAttackComponent>(args.User, out var sneak))
            return;

        var surpriseReady = sneak.SurpriseReadyAt == TimeSpan.Zero || _timing.CurTime >= sneak.SurpriseReadyAt;
        ResetSurprise(args.User, sneak);
        if (args.HitEntities.Count == 0)
            return;
        var target = args.HitEntities[0];
        if (target == args.User || args.Weapon != args.User && !args.BaseDamage.DamageDict.ContainsKey("Slash"))
            return;

        if (!surpriseReady)
        {
            if (_standing.IsDown(target))
            {
                if (args.Weapon == args.User)
                    _stamina.TakeStaminaDamage(target, 30f, source: args.User);
                if (TryComp<MeleeWeaponComponent>(args.Weapon, out var melee))
                {
                    var interval = TimeSpan.FromSeconds(1f / _melee.GetAttackRate(args.Weapon, args.User, melee));
                    var reduction = interval / 2f;
                    if (interval - reduction > TimeSpan.FromSeconds(0.125f))
                    {
                        melee.NextAttack -= reduction;
                        Dirty(args.Weapon, melee);
                    }
                }
            }
            return;
        }

        args.BonusDamage = args.BaseDamage * (sneak.Multiplier - 1f);
        if (args.Direction == null)
        {
            var unarmed = args.Weapon == args.User;
            var damage = new DamageSpecifier
            {
                DamageDict =
                {
                    { unarmed ? "Blunt" : "Slash", unarmed ? sneak.AssassinateUnarmedDamage : sneak.AssassinateDamage },
                },
            };
            _damage.TryChangeDamage(target, damage, origin: args.User);
            _audio.PlayPvs(unarmed ? sneak.AssassinateSoundUnarmed : sneak.AssassinateSoundArmed, target);
            ComboPopup(args.User, target, "Assassinate");
        }
    }

    private void OnInteractHand(InteractHandEvent args)
    {
        if (args.User == args.Target
            || !HasComp<MobStateComponent>(args.Target)
            || !TryComp<NinjutsuSneakAttackComponent>(args.User, out var sneak))
            return;
        if (sneak.SurpriseReadyAt != TimeSpan.Zero && _timing.CurTime < sneak.SurpriseReadyAt)
        {
            _popup.PopupEntity(Loc.GetString("ninjutsu-fail-loss-of-surprise"), args.User, args.User);
            ResetSurprise(args.User, sneak);
            return;
        }
        ResetSurprise(args.User, sneak);
        if (_standing.IsDown(args.Target))
            return;
        _movement.TryUpdateMovementSpeedModDuration(args.Target, SlowdownEffect, TimeSpan.FromSeconds(sneak.TakedownSlowdownTime), sneak.TakedownSpeedModifier);
        EnsureComp<KravMagaSilencedComponent>(args.Target).Until = _timing.CurTime + TimeSpan.FromSeconds(sneak.TakedownMuteTime);
        _audio.PlayPvs(sneak.AssassinateSoundUnarmed, args.Target);
        ComboPopup(args.User, args.Target, "Ninjutsu-Takedown");
    }

    private void OnNinjutsuStartup(Entity<NinjutsuSneakAttackComponent> ent, ref ComponentStartup args)
        => _alerts.ShowAlert(ent.Owner, SneakAttackAlert);

    private void OnNinjutsuRemove(Entity<NinjutsuSneakAttackComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent))
            _alerts.ClearAlertCategory(ent.Owner, NinjutsuAlertCategory);
    }

    private void OnNinjutsuGunshot(Entity<NinjutsuSneakAttackComponent> ent, ref SelfBeforeGunShotEvent args)
        => ResetSurprise(ent, ent.Comp);

    private void OnThrown(ref ThrownEvent args)
    {
        if (args.User is { } user && TryComp<NinjutsuSneakAttackComponent>(user, out var sneak))
            ResetSurprise(user, sneak);
    }

    private void ResetSurprise(EntityUid uid, NinjutsuSneakAttackComponent sneak)
    {
        sneak.SurpriseReadyAt = _timing.CurTime + TimeSpan.FromSeconds(5);
        _alerts.ShowAlert(uid, LossOfSurpriseAlert, cooldown: (_timing.CurTime, sneak.SurpriseReadyAt));
    }

    private void OnAttackRate(ref GetMeleeAttackRateEvent args)
    {
        if (TryComp<MartialArtModifiersComponent>(args.User, out var modifier))
            args.Multipliers *= modifier.AttackRate;
    }

    private void OnMeleeDamage(ref GetMeleeDamageEvent args)
    {
        if (TryComp<MartialArtModifiersComponent>(args.User, out var modifier)
            && (!modifier.DamageUnarmedOnly || args.Weapon == args.User))
            args.Damage *= modifier.Damage;
    }

    private void OnMoveSpeed(Entity<MartialArtModifiersComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
        => args.ModifySpeed(ent.Comp.MoveSpeed, ent.Comp.MoveSpeed);

    private void OnDragonMeleeModifiers(Entity<DragonKungFuComponent> ent, ref GetMeleeTargetModifiersEvent args)
    {
        if (_timing.CurTime >= ent.Comp.BuffUntil
            || _hands.TryGetActiveItem(ent.Owner, out _)
            || !_actionBlocker.CanInteract(ent, null))
            return;

        args.Modifiers.Add(DragonPowerResistance);
        var modifier = EnsureComp<MartialArtModifiersComponent>(ent);
        modifier.Damage = 1.2f;
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
        if (!ent.Comp.Enabled || !args.IsHit || args.HitEntities.Count == 0)
            return;
        var specialUsed = false;
        foreach (var target in args.HitEntities.Where(target => HasComp<MobStateComponent>(target)))
        {
            switch (specialUsed ? null : ent.Comp.SelectedMove)
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
            specialUsed |= ent.Comp.SelectedMove != null;
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
        {
            _popup.PopupEntity(Loc.GetString("krav-maga-cant-speak"), ent, ent);
            args.Cancel();
        }
    }
}
