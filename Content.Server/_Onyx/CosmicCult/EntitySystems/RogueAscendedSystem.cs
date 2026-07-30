// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Numerics;
using Content.Server._Onyx.CosmicCult.Abilities;
using Content.Server._Onyx.CosmicCult.Components;
using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.Light.EntitySystems;
using Content.Server.Popups;
using Content.Shared._Onyx.CosmicCult;
using Content.Shared._Onyx.CosmicCult.Components;
using Content.Shared._Onyx.CosmicCult.Components.Examine;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.CosmicCult.EntitySystems;

public sealed partial class RogueAscendedSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private HolyProtectionSystem _holy = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PoweredLightSystem _poweredLight = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId RogueNovaProjectile = "ProjectileRogueCosmicNova";
    private static readonly EntProtoId SpawnWisp = "MobCosmicWisp";
    private static readonly EntProtoId BlankVfx = "CosmicBlankAbilityVFX";
    private static readonly EntProtoId GlareVfx = "CosmicGlareAbilityVFX";
    private static readonly SoundSpecifier BlankSfx = new SoundPathSpecifier("/Audio/_Onyx/CosmicCult/ability_blank.ogg");
    private static readonly SoundSpecifier ShuntSfx = new SoundPathSpecifier("/Audio/_Onyx/CosmicCult/ascendant_shunt.ogg");
    private static readonly SoundSpecifier NovaSfx = new SoundPathSpecifier("/Audio/_Onyx/CosmicCult/ability_nova_cast.ogg");

    private readonly HashSet<Entity<PoweredLightComponent>> _lights = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RogueAscendedComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RogueAscendedDendriteComponent, FullyEatenEvent>(OnDendriteConsumed);
        SubscribeLocalEvent<RogueAscendedComponent, EventRogueInfection>(OnAttemptInfection);
        SubscribeLocalEvent<RogueAscendedComponent, EventRogueInfectionDoAfter>(OnInfectionDoAfter);
        SubscribeLocalEvent<RogueAscendedComponent, EventRogueSlumber>(OnAttemptSlumber);
        SubscribeLocalEvent<RogueAscendedComponent, EventRogueSlumberDoAfter>(OnSlumberDoAfter);
        SubscribeLocalEvent<RogueAscendedComponent, EventRogueCosmicNova>(OnRogueNova);
        SubscribeLocalEvent<RogueAscendedAuraComponent, EventRogueCosmicNova>(OnEmpoweredNova);
        SubscribeLocalEvent<RogueAscendedComponent, EventRogueGrandShunt>(OnRogueShunt);
    }

    private void OnMobStateChanged(Entity<RogueAscendedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            _audio.PlayPvs(ent.Comp.MobSound, ent);
    }

    private void OnDendriteConsumed(Entity<RogueAscendedDendriteComponent> ent, ref FullyEatenEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.User))
            return;

        if (TryComp<CosmicCultComponent>(args.User, out var cult))
        {
            cult.EntropyBudget += 30;
            cult.CosmicEmpowered = true;
            Dirty(args.User, cult);
            return;
        }

        if (HasComp<RogueAscendedAuraComponent>(args.User))
            return;

        Spawn(ent.Comp.Vfx, Transform(args.User).Coordinates);
        EnsureComp<RogueAscendedAuraComponent>(args.User);
        _actions.AddAction(args.User, ref ent.Comp.RogueFoodActionEntity, ent.Comp.RogueFoodAction, args.User);
        _audio.PlayPvs(ent.Comp.ActivateSfx, args.User);
        _popup.PopupCoordinates(Loc.GetString("rogue-ascended-dendrite-eaten"), Transform(args.User).Coordinates, PopupType.Medium);
        _color.RaiseEffect(Color.CadetBlue, [args.User], Filter.Pvs(args.User, entityManager: EntityManager));
        _stun.TryKnockdown(args.User, ent.Comp.StunTime, false);
    }

    private void OnAttemptSlumber(Entity<RogueAscendedComponent> ent, ref EventRogueSlumber args)
    {
        if (args.Handled || !CanTarget(args.Target, requireSleeping: false))
        {
            _popup.PopupEntity(Loc.GetString("rogue-ascended-shatter-fail"), ent, ent);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.RogueSlumberDoAfterTime,
            new EventRogueSlumberDoAfter(), ent, args.Target)
        {
            DistanceThreshold = 2f,
            Hidden = false,
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnDropItem = true,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
    }

    private void OnSlumberDoAfter(Entity<RogueAscendedComponent> ent, ref EventRogueSlumberDoAfter args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target || !CanTarget(target, requireSleeping: false))
            return;

        _statusEffects.TrySetStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, ent.Comp.RogueSlumberTime);
        _audio.PlayPvs(ent.Comp.ShatterSfx, target);
        Spawn(ent.Comp.Vfx, Transform(target).Coordinates);
        args.Handled = true;
    }

    private void OnAttemptInfection(Entity<RogueAscendedComponent> ent, ref EventRogueInfection args)
    {
        if (args.Handled || !CanTarget(args.Target, requireSleeping: true))
        {
            _popup.PopupEntity(Loc.GetString("rogue-ascended-infection-fail"), ent, ent);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.RogueInfectionDoAfterTime,
            new EventRogueInfectionDoAfter(), ent, args.Target)
        {
            DistanceThreshold = 2f,
            Hidden = false,
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnDropItem = true,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
        _audio.PlayPvs(ent.Comp.MobSound, ent);
        _popup.PopupCoordinates(
            Loc.GetString("rogue-ascended-infection-notification",
                ("target", Identity.Entity(args.Target, EntityManager)),
                ("user", Identity.Entity(args.Performer, EntityManager))),
            Transform(ent).Coordinates,
            PopupType.LargeCaution);
    }

    private void OnInfectionDoAfter(Entity<RogueAscendedComponent> ent, ref EventRogueInfectionDoAfter args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target || !CanTarget(target, requireSleeping: true))
            return;

        EnsureComp<RogueAscendedInfectionComponent>(target);
        Spawn(ent.Comp.Vfx, Transform(target).Coordinates);
        _antag.SendBriefing(target, Loc.GetString("rogue-ascended-infection-briefing"), Color.FromHex("#4cabb3"), null);
        _damageable.TryChangeDamage(target, -ent.Comp.InfectionHeal, origin: ent);
        _stun.TryUpdateParalyzeDuration(target, ent.Comp.StunTime);
        _audio.PlayPvs(ent.Comp.InfectionSfx, target);

        if (_mind.TryGetObjectiveComp<RogueInfectionConditionComponent>(ent, out var objective))
            objective.MindsCorrupted++;

        args.Handled = true;
    }

    private bool CanTarget(EntityUid target, bool requireSleeping)
    {
        if (!HasComp<HumanoidProfileComponent>(target) ||
            !TryComp<MobStateComponent>(target, out var state) || state.CurrentState != MobState.Alive ||
            !TryComp<MindContainerComponent>(target, out var mind) || mind.Mind == null ||
            HasComp<RogueAscendedInfectionComponent>(target) ||
            HasComp<CosmicCultComponent>(target) ||
            _holy.ShouldDeny(target))
            return false;

        return !requireSleeping || _statusEffects.HasStatusEffect(target, SleepingSystem.StatusEffectForcedSleeping);
    }

    private void OnRogueNova(Entity<RogueAscendedComponent> ent, ref EventRogueCosmicNova args)
    {
        CastNova(ent, ref args);
    }

    private void OnEmpoweredNova(Entity<RogueAscendedAuraComponent> ent, ref EventRogueCosmicNova args)
    {
        CastNova(ent, ref args);
    }

    private void CastNova(EntityUid uid, ref EventRogueCosmicNova args)
    {
        if (args.Handled)
            return;

        var start = _transform.GetMapCoordinates(args.Performer);
        var target = _transform.ToMapCoordinates(args.Target);
        var velocity = _physics.GetMapLinearVelocity(args.Performer);
        var delta = target.Position - start.Position;
        if (delta.LengthSquared() < 0.000001f)
            delta = new Vector2(.01f, 0f);

        args.Handled = true;
        var projectile = Spawn(RogueNovaProjectile, start);
        _gun.ShootProjectile(projectile, delta, velocity, args.Performer, args.Performer, 5f);
        _audio.PlayPvs(NovaSfx, uid, AudioParams.Default.WithVariation(0.1f));
    }

    private void OnRogueShunt(Entity<RogueAscendedComponent> ent, ref EventRogueGrandShunt args)
    {
        if (args.Handled)
            return;

        var spawnPoints = EntityManager.GetAllComponents(typeof(CosmicVoidSpawnComponent)).ToImmutableList();
        if (spawnPoints.IsEmpty)
            return;

        args.Handled = true;
        Spawn(GlareVfx, Transform(ent).Coordinates);

        _lights.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 10f, _lights);
        foreach (var light in _lights)
            _poweredLight.TryDestroyBulb(light);

        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, 10f))
        {
            if (target.Owner == ent.Owner || target.Comp.CurrentState == MobState.Dead ||
                !HasComp<HumanoidProfileComponent>(target) || _holy.ShouldDeny(target) ||
                !_interaction.InRangeUnobstructed((ent.Owner, Transform(ent)), (target.Owner, Transform(target)),
                    range: 10f, collisionMask: CollisionGroup.Impassable) ||
                !TryComp<MindContainerComponent>(target, out var mindContainer) || mindContainer.Mind is not { } mindEnt)
                continue;

            var bodyCoordinates = Transform(target).Coordinates;
            var marker = _random.Pick(spawnPoints);
            var voidCoordinates = Transform(marker.Uid).Coordinates;
            var wisp = Spawn(SpawnWisp, voidCoordinates);
            var mind = Comp<MindComponent>(mindEnt);
            mind.PreventGhosting = true;

            EnsureComp<CosmicBlankComponent>(target);
            var examine = EnsureComp<CosmicCultExamineComponent>(target);
            examine.CultistText = "cosmic-examine-text-abilityblank";
            var inVoid = EnsureComp<InVoidComponent>(wisp);
            inVoid.OriginalBody = target;
            inVoid.ExitVoidTime = _timing.CurTime + TimeSpan.FromSeconds(14);

            _mind.TransferTo(mindEnt, wisp);
            _stun.TryKnockdown(target.Owner, TimeSpan.FromSeconds(16), true);
            _popup.PopupEntity(Loc.GetString("cosmicability-blank-transfer"), wisp, wisp);
            _audio.PlayLocal(BlankSfx, wisp, wisp, AudioParams.Default.WithVolume(6f));
            _color.RaiseEffect(Color.CadetBlue, [target.Owner], Filter.Pvs(target, entityManager: EntityManager));
            Spawn(BlankVfx, bodyCoordinates);
            Spawn(BlankVfx, voidCoordinates);
        }

        _audio.PlayPvs(ShuntSfx, ent, AudioParams.Default.WithVolume(6f));
    }
}
