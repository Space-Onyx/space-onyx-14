/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Onyx.ZLevels.Core.Components;
using Content.Shared._Onyx.ZLevels.Ghost;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Onyx.ZLevels.Core.EntitySystems;

/// <summary>
/// Raised on a z-physics entity when it wakes (becomes active) or sleeps (becomes inactive).
/// Replaces what used to be <c>CEActiveZPhysicsComponent</c> ComponentInit / ComponentShutdown.
/// </summary>
[ByRefEvent]
public readonly record struct CEZPhysicsActivationChangedEvent(bool Active);

public abstract partial class CESharedZLevelsSystem
{
    private static readonly TimeSpan StartupActivationDelay = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Entities eligible for Z gameplay. Dormant members remain here for visuals/actions but are
    /// omitted from fixed-step processing until an invalidating event wakes them.
    /// Membership is mutated only through <see cref="WakeBody"/> / <see cref="SleepBody"/>.
    /// </summary>
    private readonly List<EntityUid> _activeBodies = new();
    private readonly HashSet<EntityUid> _activeBodySet = new();

    /// <summary>
    /// Gameplay-active bodies that currently require fixed-step processing. Stable grounded bodies
    /// remain in <see cref="_activeBodies"/> but leave this list until an invalidating event wakes them.
    /// </summary>
    private readonly List<EntityUid> _processingBodies = new();
    private readonly HashSet<EntityUid> _processingBodySet = new();
    private readonly HashSet<EntityUid> _dormantBodies = new();
    private readonly Dictionary<EntityUid, byte> _restingSteps = new();
    private const byte RestingStepsBeforeDormancy = 3;

    /// <summary>
    /// Entities whose movement cache will be refreshed at the start of the next physics update.
    /// Used to deduplicate cache work when many entities are invalidated at once (e.g. tile
    /// changes hitting an AABB full of bodies, or a grid moving its children).
    /// </summary>
    private readonly HashSet<EntityUid> _dirtyMovementBodies = new();

    [PublicAPI]
    public IReadOnlyList<EntityUid> ActiveBodies => _activeBodies;

    [ViewVariables]
    public int ProcessingBodyCount => _processingBodies.Count;

    [ViewVariables]
    public int DormantBodyCount => _activeBodies.Count - _processingBodies.Count;

    [PublicAPI]
    public bool IsBodyActive(EntityUid uid) => _activeBodySet.Contains(uid);

    [PublicAPI]
    public bool IsBodyProcessing(EntityUid uid) => _processingBodySet.Contains(uid);

    /// <summary>
    /// Queues a coalesced movement-cache refresh, drained at the start of the next physics update.
    /// Use when many bodies are invalidated at once; synchronous callers needing the cache current
    /// before their next read should call <see cref="CacheMovement"/> directly.
    /// </summary>
    [PublicAPI]
    public void DirtyMovement(EntityUid uid)
    {
        _dirtyMovementBodies.Add(uid);
        WakeProcessing(uid);
    }

    /// <summary>Drains the dirty-movement queue, refreshing each body's cache once.</summary>
    protected void UpdateDirtyMovement()
    {
        foreach (var uid in _dirtyMovementBodies)
        {
            if (ZPhysQuery.TryComp(uid, out var zPhys))
                CacheMovement((uid, zPhys));
        }

        _dirtyMovementBodies.Clear();
    }

    private void InitializeActivation()
    {
        SubscribeLocalEvent<CEZPhysicsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEZPhysicsComponent, AnchorStateChangedEvent>(OnAnchorStateChange);
        SubscribeLocalEvent<CEZPhysicsComponent, PhysicsBodyTypeChangedEvent>(OnPhysicsBodyTypeChange);
        SubscribeLocalEvent<CEZPhysicsComponent, PhysicsWakeEvent>(OnPhysicsWake);
        SubscribeLocalEvent<CEZPhysicsComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<GravityChangedEvent>(OnGravityChanged);
        SubscribeLocalEvent<CEZLevelGhostMoverComponent, ComponentStartup>(OnGhostMoverStartup);
        SubscribeLocalEvent<CEZLevelGhostMoverComponent, ComponentShutdown>(OnGhostMoverShutdown);
        // Becoming/leaving a ghost flips IsAutomaticZPhysicsExcluded; refresh activation so the
        // body sleeps/wakes. Hooked via ComponentAdded/Removed because GhostComponent's exclusive
        // ComponentStartup is already owned by GhostSystem.
        EntityManager.ComponentAdded += OnComponentAddedForActivation;
        EntityManager.ComponentRemoved += OnComponentRemovedForActivation;
    }

    private void ShutdownActivation()
    {
        EntityManager.ComponentAdded -= OnComponentAddedForActivation;
        EntityManager.ComponentRemoved -= OnComponentRemovedForActivation;
    }

    private void OnComponentAddedForActivation(AddedComponentEventArgs ev)
    {
        if (ev.BaseArgs.Component is GhostComponent)
            RefreshZPhysicsActivation(ev.BaseArgs.Owner);
    }

    private void OnComponentRemovedForActivation(RemovedComponentEventArgs ev)
    {
        if (ev.BaseArgs.Component is GhostComponent)
            RefreshZPhysicsActivation(ev.BaseArgs.Owner);
    }

    private void OnAnchorStateChange(Entity<CEZPhysicsComponent> ent, ref AnchorStateChangedEvent args)
    {
        RefreshBody(ent);
    }

    private void OnMapInit(Entity<CEZPhysicsComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.StartupSuppressedUntil = _timing.CurTime + StartupActivationDelay;
        RefreshBody(ent);

        if (!TryGetTraversalDepth(Transform(ent), out var depth))
            return;

        ent.Comp.CurrentZLevel = depth;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.CurrentZLevel));
    }

    private void OnPhysicsBodyTypeChange(Entity<CEZPhysicsComponent> ent, ref PhysicsBodyTypeChangedEvent args)
    {
        RefreshBody(ent);
    }

    private void OnPhysicsWake(Entity<CEZPhysicsComponent> ent, ref PhysicsWakeEvent args)
    {
        WakeProcessing(ent);
    }

    private void OnGravityChanged(ref GravityChangedEvent args)
    {
        foreach (var uid in _activeBodies)
        {
            if (_processingBodySet.Contains(uid) || !TransformQuery.TryComp(uid, out var xform))
                continue;
            if (xform.GridUid == args.ChangedGridIndex || xform.MapUid == args.ChangedGridIndex)
                WakeProcessing(uid);
        }
    }

    private void OnParentChanged(Entity<CEZPhysicsComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshBody(ent);

        var xform = Transform(ent);

        if (_net.IsClient && !_timing.ApplyingState)
            return;

        var oldParentWorld = GetEntityWorldPositionCsv(args.OldParent);
        var oldParentVelocity = GetEntityVelocityCsv(args.OldParent);
        var newParentUid = xform.ParentUid;
        var newParentWorld = GetEntityWorldPositionCsv(newParentUid);
        var newParentVelocity = GetEntityVelocityCsv(newParentUid);

        DebugZStairCsv(ent,
            "parent_change",
            $"old_parent={args.OldParent},old_parent_world={oldParentWorld},old_parent_vel={oldParentVelocity},new_parent={newParentUid},new_parent_world={newParentWorld},new_parent_vel={newParentVelocity},new_grid={xform.GridUid},new_map={xform.MapUid}");

        if (ZPhysQuery.TryComp(args.OldParent, out var oldParentZPhys))
            SetZPosition((ent, ent), oldParentZPhys.LocalPosition);
    }

    private void OnGhostMoverStartup(Entity<CEZLevelGhostMoverComponent> ent, ref ComponentStartup args)
    {
        RefreshZPhysicsActivation(ent);
    }

    private void OnGhostMoverShutdown(Entity<CEZLevelGhostMoverComponent> ent, ref ComponentShutdown args)
    {
        RefreshZPhysicsActivation(ent);
    }

    private void RefreshZPhysicsActivation(EntityUid uid)
    {
        if (!ZPhysQuery.TryComp(uid, out var zPhys))
            return;

        RefreshBody((uid, zPhys));
    }

    private bool IsAutomaticZPhysicsExcluded(EntityUid uid)
    {
        return HasComp<GhostComponent>(uid) ||
               HasComp<CEZLevelGhostMoverComponent>(uid) ||
               HasComp<CEZLevelPhysicsExemptComponent>(uid) || // Pirate: multiz - free-floating camera eyes
               _container.IsEntityInContainer(uid); // Pirate: multiz - contained entities (e.g. mech pilot) ride their holder, never fall independently
    }

    /// <summary>
    /// Re-evaluates whether <paramref name="ent"/> should be in the active list and dispatches
    /// to <see cref="WakeBody"/> or <see cref="SleepBody"/>.
    /// </summary>
    [PublicAPI]
    public void RefreshBody(Entity<CEZPhysicsComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
        {
            SleepBody(ent);
            return;
        }

        if (IsAutomaticZPhysicsExcluded(ent))
        {
            SleepBody(ent);
            return;
        }

        var xform = Transform(ent);

        if (!HasTraversalContext(xform))
        {
            SleepBody(ent);
            return;
        }

        if (xform.ParentUid != xform.MapUid && xform.ParentUid != xform.GridUid)
        {
            DebugZ(ent, "z-physics inactive: parent is neither the map nor the grid");
            SleepBody(ent);
            return;
        }

        if (xform.Anchored)
        {
            DebugZ(ent, "z-physics inactive: entity is anchored");
            SleepBody(ent);
            return;
        }

        if (PhysicsQuery.TryComp(ent, out var physics) && physics.BodyType == BodyType.Static)
        {
            DebugZ(ent, "z-physics inactive: body type is static");
            SleepBody(ent);
            return;
        }

        DebugZ(ent, "z-physics active");
        WakeBody(ent);
    }

    /// <summary>
    /// Adds the entity to the active list and primes its movement cache. No-op if already active.
    /// </summary>
    [PublicAPI]
    public void WakeBody(Entity<CEZPhysicsComponent> ent)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_activeBodySet.Contains(ent))
        {
            WakeProcessing(ent);
            return;
        }

        _activeBodies.Add(ent);
        _activeBodySet.Add(ent);
        WakeProcessing(ent);

        CacheMovement(ent);

        var ev = new CEZPhysicsActivationChangedEvent(true);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    /// Removes the entity from the active list. No-op if it wasn't active.
    /// </summary>
    [PublicAPI]
    public void SleepBody(EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!_activeBodySet.Remove(uid))
            return;

        _activeBodies.Remove(uid);
        _processingBodySet.Remove(uid);
        _processingBodies.Remove(uid);
        _dormantBodies.Remove(uid);
        _restingSteps.Remove(uid);

        SetZGravityInfluenced(uid, false);

        var ev = new CEZPhysicsActivationChangedEvent(false);
        RaiseLocalEvent(uid, ref ev);
    }

    private void WakeProcessing(EntityUid uid)
    {
        if (!_activeBodySet.Contains(uid) || !_processingBodySet.Add(uid))
        {
            _restingSteps.Remove(uid);
            _dormantBodies.Remove(uid);
            return;
        }

        _processingBodies.Add(uid);
        _dormantBodies.Remove(uid);
        _restingSteps.Remove(uid);
    }

    private void UpdateDormancy(EntityUid uid, bool stableGrounded)
    {
        if (!stableGrounded)
        {
            _restingSteps.Remove(uid);
            return;
        }

        var steps = (byte) (_restingSteps.GetValueOrDefault(uid) + 1);
        if (steps < RestingStepsBeforeDormancy)
        {
            _restingSteps[uid] = steps;
            return;
        }

        _restingSteps.Remove(uid);
        _dormantBodies.Add(uid);
    }

    private void ApplyDormancy()
    {
        if (_dormantBodies.Count == 0)
            return;

        foreach (var uid in _dormantBodies)
            _processingBodySet.Remove(uid);

        var write = 0;
        for (var read = 0; read < _processingBodies.Count; read++)
        {
            var uid = _processingBodies[read];
            if (_dormantBodies.Contains(uid))
                continue;
            _processingBodies[write++] = uid;
        }

        if (write < _processingBodies.Count)
            _processingBodies.RemoveRange(write, _processingBodies.Count - write);
        _dormantBodies.Clear();
    }
}
