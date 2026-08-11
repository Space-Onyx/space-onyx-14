// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 ss14-Starlight <ss14-Starlight@outlook.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Linq;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Network;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.VentCrawling;

public sealed partial class SharedVentCrawableSystem : EntitySystem
{
    private static readonly Direction[] VentDirections = [Direction.North, Direction.South, Direction.East, Direction.West];

    [Dependency] private SharedVentTubeSystem _ventTubeSystem = default!;
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VentCrawlerHolderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VentCrawlerHolderComponent, MoveInputEvent>(OnMoveInput);
    }

    private void OnMoveInput(EntityUid uid, VentCrawlerHolderComponent holder, ref MoveInputEvent args)
    {
        if (!Exists(holder.CurrentTube))
        {
            var ev = new VentCrawlingExitEvent();
            RaiseLocalEvent(uid, ref ev);
        }

        var buttons = args.Entity.Comp.HeldMoveButtons;
        var pressed = buttons & ~args.OldMovement;
        if (TryChangePipeLayer(uid, holder, pressed))
        {
            SetMovementInput(holder, MoveButtons.None);
            holder.DirectionQueued = false;
            if (!_net.IsClient)
                Dirty(uid, holder);
            return;
        }

        var pressedDirection = GetDirection(SharedMoverController.GetNormalizedMovement(pressed), Direction.Invalid);
        if (pressedDirection != Direction.Invalid)
        {
            holder.CurrentDirection = pressedDirection;
            holder.DirectionQueued = true;
        }
        else if (holder.NextTube == null && !holder.DirectionQueued)
            SetMovementInput(holder, buttons);

        holder.IsMoving = (buttons & MoveButtons.AnyDirection) != MoveButtons.None;
    }

    private bool TryChangePipeLayer(EntityUid holderUid, VentCrawlerHolderComponent holder, MoveButtons buttons)
    {
        if (holder.NextTube != null ||
            holder.CurrentTube is not { } tube ||
            !TryComp<VentCrawlerLayerTransitionComponent>(tube, out var transition) ||
            transition.Layers == 0)
            return false;

        var axis = Transform(tube).LocalRotation.GetDir();
        var vertical = axis is Direction.North or Direction.South;
        var previous = (buttons & (vertical ? MoveButtons.Left : MoveButtons.Down)) != 0;
        var next = (buttons & (vertical ? MoveButtons.Right : MoveButtons.Up)) != 0;
        if (previous == next)
            return false;

        var step = next ? 1 : -1;
        var layer = (int) holder.PipeLayer + step;
        var selectedLayer = (AtmosPipeLayer) ((layer + transition.Layers) % transition.Layers);
        if (_net.IsClient)
        {
            if (_timing.IsFirstTimePredicted)
            {
                var ev = new VentCrawlerLayerSelectedEvent(step, transition.Layers);
                RaiseLocalEvent(holderUid, ref ev);
            }
        }
        else
        {
            holder.PipeLayer = selectedLayer;
            holder.LayerSelectionSequence++;
            UpdateVisibleTubes(holder);
        }
        if (!_net.IsClient && holder.Container.ContainedEntities.Count > 0)
        {
            var crawler = holder.Container.ContainedEntities[0];
            var layerName = Loc.GetString($"atmos-pipe-layers-component-layer-{selectedLayer.ToString().ToLowerInvariant()}");
            _popup.PopupEntity(Loc.GetString("ventcrawling-pipe-layer-changed", ("layer", layerName)), holderUid, crawler);
        }
        return true;
    }

    public void SetMovementInput(VentCrawlerHolderComponent holder, MoveButtons buttons)
    {
        var movement = SharedMoverController.GetNormalizedMovement(buttons);
        holder.CurrentDirection = GetDirection(movement, holder.CurrentDirection);
        holder.IsMoving = holder.CurrentDirection != Direction.Invalid;
        if (!holder.IsMoving)
            StopCrawlSound(holder);
    }

    private void OnStartup(EntityUid uid, VentCrawlerHolderComponent holder, ComponentStartup args)
        => holder.Container = _containerSystem.EnsureContainer<Container>(uid, nameof(VentCrawlerHolderComponent));

    public bool TryInsert(EntityUid uid, EntityUid toInsert, VentCrawlerHolderComponent? holder = null)
    {
        if (!Resolve(uid, ref holder)
            || !_containerSystem.CanInsert(toInsert, holder.Container)
            || !_containerSystem.Insert(toInsert, holder.Container))
            return false;

        if (TryComp<PhysicsComponent>(toInsert, out var body))
            _physicsSystem.SetCanCollide(toInsert, false, body: body);

        return true;
    }

    public bool EnterTube(EntityUid holderUid,
        EntityUid toUid,
        VentCrawlerHolderComponent? holder = null,
        TransformComponent? holderTransform = null,
        VentCrawlerTubeComponent? tube = null,
        TransformComponent? tubeTransform = null)
    {
        if (!Resolve(holderUid, ref holder, ref holderTransform))
            return false;

        if (holder.IsExitingVentCraws)
        {
            Log.Error("Tried entering tube after exiting vent craws.");
            return false;
        }

        if (!Resolve(toUid, ref tube, ref tubeTransform))
        {
            RaiseExit(holderUid);
            return false;
        }

        foreach (var entity in holder.Container.ContainedEntities)
            EnsureComp<BeingVentCrawlerComponent>(entity).Holder = holderUid;

        if (TryComp<PhysicsComponent>(holderUid, out var body))
            _physicsSystem.SetCanCollide(holderUid, false, body: body);

        if (holder.CurrentTube != null)
        {
            holder.PreviousTube = holder.CurrentTube;
            holder.PreviousDirection = holder.CurrentDirection;
        }

        holder.CurrentTube = toUid;
        holder.PipeLayer = _ventTubeSystem.GetLayer(toUid);
        _transformSystem.SetCoordinates(holderUid, tubeTransform.Coordinates);
        UpdateVisibleTubes(holder);
        Dirty(holderUid, holder);
        return true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<VentCrawlerHolderComponent>();
        while (query.MoveNext(out var uid, out var holder))
        {
            UpdateVisibleTubes(holder);

            if (holder.CurrentTube == null || holder.CurrentDirection == Direction.Invalid && !holder.DirectionQueued)
            {
                if (!_net.IsClient)
                    StopCrawlSound(holder);
                continue;
            }

            if (holder.TilesPerSecond <= 0f)
                continue;

            var remaining = frameTime;
            while (remaining > 0f && holder.CurrentTube is { } currentTube)
            {
                var pipeLayer = holder.PipeLayer;

                if (!Exists(currentTube))
                {
                    RaiseExit(uid);
                    break;
                }

                if ((holder.IsMoving || holder.DirectionQueued) && holder.NextTube == null)
                {
                    var nextTube = _ventTubeSystem.NextTubeFor(currentTube, holder.CurrentDirection, pipeLayer);
                    if (nextTube != null)
                    {
                        holder.NextTube = nextTube;
                        holder.TravelDirection = holder.CurrentDirection;
                        holder.DirectionQueued = false;
                        holder.Progress = 0f;
                        Dirty(uid, holder);
                        StartCrawlSound(uid, holder);
                    }
                    else if (_ventTubeSystem.CanExit(currentTube,
                                 holder.CurrentDirection,
                                 holder.PipeLayer,
                                 out var exitCoordinates))
                    {
                        var ev = new VentCrawlingExitEvent { HolderTransform = Transform(uid) };
                        _transformSystem.SetCoordinates(uid, exitCoordinates);
                        RaiseLocalEvent(uid, ref ev);
                        break;
                    }
                }

                if (holder.NextTube is not { } nextTubeUid)
                {
                    StopCrawlSound(holder);
                    break;
                }

                if (!_ventTubeSystem.SupportsLayer(currentTube, pipeLayer))
                {
                    RaiseExit(uid);
                    break;
                }

                if (!Exists(nextTubeUid) || !_ventTubeSystem.SupportsLayer(nextTubeUid, pipeLayer))
                {
                    holder.NextTube = null;
                    holder.Progress = 0f;
                    _transformSystem.SetCoordinates(uid, Transform(currentTube).Coordinates);
                    StopCrawlSound(holder);
                    Dirty(uid, holder);
                    break;
                }

                var step = MathF.Min(1f - holder.Progress, remaining * holder.TilesPerSecond);
                holder.Progress += step;
                remaining -= step / holder.TilesPerSecond;
                var origin = Transform(currentTube).Coordinates;
                var target = Transform(nextTubeUid).Coordinates;
                var position = GetSegmentPosition(currentTube, nextTubeUid, holder, pipeLayer, origin, target);
                var holderTransform = Transform(uid);
                if (holderTransform.ParentUid == origin.EntityId)
                    _transformSystem.SetLocalPosition(uid, position, holderTransform);
                else
                    _transformSystem.SetCoordinates(uid, origin.WithPosition(position));

                if (holder.Progress < 1f)
                    break;

                holder.PreviousTube = currentTube;
                holder.PreviousDirection = holder.TravelDirection;
                holder.CurrentTube = nextTubeUid;
                holder.NextTube = null;
                holder.TravelDirection = Direction.Invalid;
                holder.Progress = 0f;
                UpdateVisibleTubes(holder);
                Dirty(uid, holder);

                var welded = TryComp<WeldableComponent>(nextTubeUid, out var weldable) && weldable.IsWelded;
                if (HasComp<VentCrawlerEntryComponent>(nextTubeUid) && !holder.FirstEntry && !welded)
                {
                    StopCrawlSound(holder);
                    RaiseExit(uid);
                    break;
                }

                holder.FirstEntry = false;
            }
        }
    }

    private void UpdateVisibleTubes(VentCrawlerHolderComponent holder)
    {
        if (_net.IsClient || holder.CurrentTube is not { } currentTube || !Exists(currentTube))
            return;

        var center = _transformSystem.GetWorldPosition(currentTube);
        var visible = new HashSet<EntityUid> { currentTube };
        var pending = new Queue<EntityUid>();
        pending.Enqueue(currentTube);

        while (pending.TryDequeue(out var tube))
        {
            foreach (var direction in VentDirections)
            {
                if (_ventTubeSystem.NextTubeFor(tube, direction, holder.PipeLayer) is not { } next ||
                    !visible.Add(next))
                    continue;

                var offset = _transformSystem.GetWorldPosition(next) - center;
                if (MathF.Abs(offset.X) > 1f || MathF.Abs(offset.Y) > 1f)
                {
                    visible.Remove(next);
                    continue;
                }

                pending.Enqueue(next);
            }
        }

        foreach (var crawlerUid in holder.Container.ContainedEntities)
        {
            if (!TryComp<VentCrawlerComponent>(crawlerUid, out var crawler) ||
                crawler.VisibleTubes.Count == visible.Count && crawler.VisibleTubes.All(visible.Contains))
                continue;

            crawler.VisibleTubes = visible.ToList();
            Dirty(crawlerUid, crawler);
        }
    }

    private Vector2 GetSegmentPosition(EntityUid currentTube,
        EntityUid nextTube,
        VentCrawlerHolderComponent holder,
        AtmosPipeLayer pipeLayer,
        EntityCoordinates origin,
        EntityCoordinates target)
    {
        var progress = holder.Progress;
        if (holder.CurrentDirection == holder.TravelDirection ||
            _ventTubeSystem.NextTubeFor(nextTube, holder.CurrentDirection, pipeLayer) is not { } followingTube ||
            followingTube == currentTube)
            return Vector2.Lerp(origin.Position, target.Position, progress);

        var following = Transform(followingTube).Coordinates;
        if (following.EntityId != origin.EntityId)
            return Vector2.Lerp(origin.Position, target.Position, progress);

        var control1 = origin.Position + (target.Position - origin.Position) / 3f;
        var control2 = target.Position - (following.Position - target.Position) / 3f;
        var inverse = 1f - progress;
        return inverse * inverse * inverse * origin.Position +
               3f * inverse * inverse * progress * control1 +
               3f * inverse * progress * progress * control2 +
               progress * progress * progress * target.Position;
    }

    public void StopCrawlSound(VentCrawlerHolderComponent holder)
    {
        holder.CrawlSoundEntity = _audioSystem.Stop(holder.CrawlSoundEntity);
    }

    private void StartCrawlSound(EntityUid uid, VentCrawlerHolderComponent holder)
    {
        if (holder.CrawlSoundEntity != null)
            return;

        holder.CrawlSoundEntity = _audioSystem.PlayPvs(
            holder.CrawlSound,
            uid,
            holder.CrawlSound.Params.WithLoop(true))?.Entity;
    }

    private void RaiseExit(EntityUid uid)
    {
        var ev = new VentCrawlingExitEvent();
        RaiseLocalEvent(uid, ref ev);
    }

    private static Direction GetDirection(MoveButtons movement, Direction current)
    {
        if (current == Direction.North && (movement & MoveButtons.Up) != 0)
            return current;
        if (current == Direction.South && (movement & MoveButtons.Down) != 0)
            return current;
        if (current == Direction.West && (movement & MoveButtons.Left) != 0)
            return current;
        if (current == Direction.East && (movement & MoveButtons.Right) != 0)
            return current;
        if ((movement & MoveButtons.Up) != 0)
            return Direction.North;
        if ((movement & MoveButtons.Down) != 0)
            return Direction.South;
        if ((movement & MoveButtons.Left) != 0)
            return Direction.West;
        if ((movement & MoveButtons.Right) != 0)
            return Direction.East;
        return Direction.Invalid;
    }
}
