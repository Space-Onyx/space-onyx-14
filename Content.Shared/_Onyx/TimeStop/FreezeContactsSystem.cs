using System.Linq;
using System.Numerics;
using Content.Shared._Onyx.TimedDespawn;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Emoting;
using Content.Shared.Guardian.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Speech;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Shared._Onyx.TimeStop;

public sealed partial class FreezeContactsSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> FrozenIgnoreMindActionTag = "FrozenIgnoreMindAction";
    private const string ProjectileFixture = "projectile";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FreezeContactsComponent, StartCollideEvent>(OnEntityEnter);
        SubscribeLocalEvent<FreezeContactsComponent, EndCollideEvent>(OnEntityExit);
        SubscribeLocalEvent<FrozenComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<FrozenComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<FrozenComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<FrozenComponent, InteractionAttemptEvent>(OnInteractAttempt);
        SubscribeLocalEvent<FrozenComponent, ComponentStartup>(MoveUpdate);
        SubscribeLocalEvent<FrozenComponent, ComponentShutdown>(MoveUpdate);
        SubscribeLocalEvent<FrozenComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FrozenComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<FrozenComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<FrozenComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<FrozenComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<FrozenComponent, ChangeDirectionAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<FrozenComponent, EmoteAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<FrozenComponent, SpeakAttemptEvent>(OnAttempt);
    }

    private void OnRemove(Entity<FrozenComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp<PhysicsComponent>(ent, out var physics) && TryComp<FixturesComponent>(ent, out var fixtures))
        {
            _physics.SetAngularVelocity(ent, ent.Comp.OldAngularVelocity, false, fixtures, physics);
            _physics.SetLinearVelocity(ent, ent.Comp.OldLinearVelocity, true, true, fixtures, physics);
        }

        if (ent.Comp.HadCollisionWake)
            EnsureComp<CollisionWakeComponent>(ent);

        if (ent.Comp.FreezeTime <= 0f)
            return;

        if (_net.IsServer && TryComp<TimedDespawnComponent>(ent, out var despawn))
            despawn.Lifetime -= ent.Comp.FreezeTime;

        if (TryComp<FadingTimedDespawnComponent>(ent, out var fading) && !fading.FadeOutStarted)
            fading.Lifetime -= ent.Comp.FreezeTime;

        if (!TryComp<ThrownItemComponent>(ent, out var thrown) || thrown.LandTime == null)
            return;

        thrown.LandTime -= TimeSpan.FromSeconds(ent.Comp.FreezeTime);
        Dirty(ent, thrown);
    }

    private void OnInit(Entity<FrozenComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<PhysicsComponent>(ent, out var physics) || !TryComp<FixturesComponent>(ent, out var fixtures))
            return;

        ent.Comp.OldLinearVelocity = physics.LinearVelocity;
        ent.Comp.OldAngularVelocity = physics.AngularVelocity;
        _physics.SetAngularVelocity(ent, 0f, false, fixtures, physics);
        _physics.SetLinearVelocity(ent, Vector2.Zero, true, false, fixtures, physics);

        if (!HasComp<CollisionWakeComponent>(ent))
            return;

        ent.Comp.HadCollisionWake = true;
        RemComp<CollisionWakeComponent>(ent);
    }

    private void MoveUpdate(EntityUid uid, FrozenComponent component, EntityEventArgs args)
    {
        _blocker.UpdateCanMove(uid);
    }

    private static void OnInteractAttempt(Entity<FrozenComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private static void OnAttempt(EntityUid uid, FrozenComponent component, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private static void OnPullAttempt(EntityUid uid, FrozenComponent component, PullAttemptEvent args)
    {
        if (args.PullerUid == uid)
            args.Cancelled = true;
    }

    private static void OnUpdateCanMove(EntityUid uid, FrozenComponent component, UpdateCanMoveEvent args)
    {
        if (component.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<FrozenComponent, PhysicsComponent, FixturesComponent>();
        while (query.MoveNext(out var uid, out var frozen, out var physics, out var fixtures))
        {
            if (frozen.FreezeTime < 0f)
            {
                RemCompDeferred<FrozenComponent>(uid);
                continue;
            }

            _physics.SetAngularVelocity(uid, 0f, false, fixtures, physics);
            _physics.SetLinearVelocity(uid, Vector2.Zero, true, false, fixtures, physics);
            frozen.FreezeTime -= frameTime;
        }
    }

    private void OnEntityExit(Entity<FreezeContactsComponent> ent, ref EndCollideEvent args)
    {
        if (_net.IsClient || !ShouldCollideWith(args.OtherFixture, args.OtherFixtureId) ||
            !TryComp<PhysicsComponent>(args.OtherEntity, out var body))
        {
            return;
        }

        var query = GetEntityQuery<FreezeContactsComponent>();
        if (_physics.GetContactingEntities(args.OtherEntity, body)
            .Where(uid => uid != ent.Owner)
            .Any(query.HasComponent))
        {
            return;
        }

        RemCompDeferred<FrozenComponent>(args.OtherEntity);
    }

    private void OnEntityEnter(Entity<FreezeContactsComponent> ent, ref StartCollideEvent args)
    {
        if (!ShouldCollideWith(args.OtherFixture, args.OtherFixtureId) ||
            !TryComp<TimedDespawnComponent>(ent, out var despawn) || despawn.Lifetime <= 0f)
        {
            return;
        }

        var other = args.OtherEntity;
        if (TryComp<FrozenComponent>(other, out var frozen))
        {
            if (despawn.Lifetime <= frozen.FreezeTime)
                return;

            var difference = despawn.Lifetime - frozen.FreezeTime;
            ExtendTimers(other, difference);
            frozen.FreezeTime = despawn.Lifetime;
            return;
        }

        if (IsImmune(other) || TryComp<GuardianComponent>(other, out var guardian) &&
            guardian.Host is { } host && IsImmune(host))
        {
            return;
        }

        EnsureComp<FrozenComponent>(other).FreezeTime = despawn.Lifetime;
        ExtendTimers(other, despawn.Lifetime);
    }

    private void ExtendTimers(EntityUid uid, float duration)
    {
        if (TryComp<ThrownItemComponent>(uid, out var thrown) && thrown.LandTime != null)
        {
            thrown.LandTime += TimeSpan.FromSeconds(duration);
            thrown.Animate = false;
            Dirty(uid, thrown);
        }

        if (TryComp<TimedDespawnComponent>(uid, out var despawn))
            despawn.Lifetime += duration;
        if (TryComp<FadingTimedDespawnComponent>(uid, out var fading) && !fading.FadeOutStarted)
            fading.Lifetime += duration;
    }

    private bool IsImmune(EntityUid uid)
    {
        return _actions.GetActions(uid).Any(action => _tag.HasTag(action.Owner, FrozenIgnoreMindActionTag));
    }

    private static bool ShouldCollideWith(Fixture fixture, string id)
    {
        return fixture.Hard || id == ProjectileFixture;
    }
}
