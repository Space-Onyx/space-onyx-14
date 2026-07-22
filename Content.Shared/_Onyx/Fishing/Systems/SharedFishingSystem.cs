// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <aviu00@protonmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Fishing.Components;
using Content.Shared._Onyx.Fishing.Events;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Fishing.Systems;

public abstract partial class SharedFishingSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected ThrowingSystem Throwing = default!;
    [Dependency] protected SharedTransformSystem Xform = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IRobustRandom _random = default!;

    protected EntityQuery<ActiveFisherComponent> FisherQuery;
    protected EntityQuery<ActiveFishingSpotComponent> ActiveFishSpotQuery;
    protected EntityQuery<FishingSpotComponent> FishSpotQuery;
    protected EntityQuery<FishingRodComponent> FishRodQuery;
    protected EntityQuery<FishingLureComponent> FishLureQuery;

    public override void Initialize()
    {
        base.Initialize();

        FisherQuery = GetEntityQuery<ActiveFisherComponent>();
        ActiveFishSpotQuery = GetEntityQuery<ActiveFishingSpotComponent>();
        FishSpotQuery = GetEntityQuery<FishingSpotComponent>();
        FishRodQuery = GetEntityQuery<FishingRodComponent>();
        FishLureQuery = GetEntityQuery<FishingLureComponent>();

        SubscribeLocalEvent<FishingRodComponent, MapInitEvent>(OnFishingRodInit);
        SubscribeLocalEvent<FishingRodComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<FishingRodComponent, ThrowFishingLureActionEvent>(OnThrowFloat);
        SubscribeLocalEvent<FishingRodComponent, PullFishingLureActionEvent>(OnPullFloat);
        SubscribeLocalEvent<FishingRodComponent, EntParentChangedMessage>(OnRodParentChanged);
        SubscribeLocalEvent<FishingRodComponent, EntityTerminatingEvent>(OnRodTerminating);
        SubscribeLocalEvent<FishingLureComponent, EntityTerminatingEvent>(OnLureTerminating);
        SubscribeLocalEvent<ActiveFishingSpotComponent, EntityTerminatingEvent>(OnSpotTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateFishing();
    }

    private void UpdateFishing()
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        var currentTime = Timing.CurTime;
        var activeFishers = EntityQueryEnumerator<ActiveFisherComponent>();
        while (activeFishers.MoveNext(out var fisher, out var fisherComp))
        {
            if (TerminatingOrDeleted(fisherComp.FishingRod) ||
                !FishRodQuery.TryComp(fisherComp.FishingRod, out var fishingRodComp) ||
                TerminatingOrDeleted(fishingRodComp.FishingLure) ||
                !FishLureQuery.TryComp(fishingRodComp.FishingLure, out var fishingLureComp) ||
                TerminatingOrDeleted(fishingLureComp.AttachedEntity) ||
                !ActiveFishSpotQuery.TryComp(fishingLureComp.AttachedEntity, out var activeSpotComp))
            {
                RemCompDeferred(fisher, fisherComp);
                continue;
            }

            var fishRod = fisherComp.FishingRod;
            var fishSpot = fishingLureComp.AttachedEntity.Value;

            fisherComp.TotalProgress ??= fishingRodComp.StartingProgress;
            fisherComp.NextStruggle ??= Timing.CurTime + TimeSpan.FromSeconds(fishingRodComp.StartingStruggleTime);
            CalculateFightingTimings((fisher, fisherComp), activeSpotComp);

            switch (fisherComp.TotalProgress)
            {
                case < 0f:
                    _popup.PopupEntity(Loc.GetString("fishing-progress-fail"), fisher, fisher);
                    StopFishing((fishRod, fishingRodComp), fisher);
                    continue;
                case >= 1f:
                    if (activeSpotComp.Fish != null)
                    {
                        ThrowFishReward(activeSpotComp.Fish.Value, fishSpot, fisher);
                        _popup.PopupEntity(Loc.GetString("fishing-progress-success"), fisher, fisher);
                    }

                    StopFishing((fishRod, fishingRodComp), fisher);
                    break;
            }
        }

        var fishingSpots = EntityQueryEnumerator<ActiveFishingSpotComponent>();
        while (fishingSpots.MoveNext(out var fishingSpot, out var activeSpotComp))
        {
            if (currentTime < activeSpotComp.FishingStartTime || activeSpotComp.IsActive || activeSpotComp.FishingStartTime == null)
                continue;

            if (TerminatingOrDeleted(activeSpotComp.AttachedFishingLure) ||
                !FishLureQuery.TryComp(activeSpotComp.AttachedFishingLure, out var fishingLureComp) ||
                TerminatingOrDeleted(fishingLureComp.FishingRod) ||
                !FishRodQuery.TryComp(fishingLureComp.FishingRod, out var fishRodComp))
            {
                RemCompDeferred(fishingSpot, activeSpotComp);
                continue;
            }

            var fisher = Transform(fishingLureComp.FishingRod).ParentUid;
            if (!Exists(fisher) || TerminatingOrDeleted(fisher))
                continue;

            var activeFisher = EnsureComp<ActiveFisherComponent>(fisher);
            activeFisher.FishingRod = fishingLureComp.FishingRod;
            activeFisher.ProgressPerUse *= fishRodComp.Efficiency;
            activeFisher.TotalProgress = fishRodComp.StartingProgress;
            activeFisher.NextStruggle = Timing.CurTime + TimeSpan.FromSeconds(fishRodComp.StartingStruggleTime);
            _popup.PopupEntity(Loc.GetString("fishing-progress-start"), fisher, fisher);
            activeSpotComp.IsActive = true;
            Dirty(fisher, activeFisher);
            Dirty(fishingSpot, activeSpotComp);
        }

        var fishingLures = EntityQueryEnumerator<FishingLureComponent, TransformComponent>();
        while (fishingLures.MoveNext(out var fishingLure, out var lureComp, out var xform))
        {
            if (lureComp.NextUpdate > Timing.CurTime)
                continue;

            lureComp.NextUpdate = Timing.CurTime + TimeSpan.FromSeconds(lureComp.UpdateInterval);

            if (TerminatingOrDeleted(lureComp.FishingRod) ||
                !FishRodQuery.TryComp(lureComp.FishingRod, out var fishingRodComp))
            {
                if (Net.IsServer)
                    QueueDel(fishingLure);
                continue;
            }

            var lurePos = Xform.GetMapCoordinates(fishingLure, xform);
            var rodPos = Xform.GetMapCoordinates(lureComp.FishingRod);
            var fisher = Transform(lureComp.FishingRod).ParentUid;

            if (!Exists(fisher) || TerminatingOrDeleted(fisher) ||
                lurePos.MapId != rodPos.MapId ||
                (lurePos.Position - rodPos.Position).LengthSquared() >
                    fishingRodComp.BreakOnDistance * fishingRodComp.BreakOnDistance ||
                !_hands.IsHolding(fisher, lureComp.FishingRod) ||
                !HasComp<ActorComponent>(fisher))
            {
                StopFishing((lureComp.FishingRod, fishingRodComp), fisher);
            }
        }
    }

    private void ToggleFishingActions(Entity<FishingRodComponent> ent, EntityUid fisher, bool addPulling)
    {
        if (TerminatingOrDeleted(ent) || !Exists(fisher) || TerminatingOrDeleted(fisher))
            return;

        if (addPulling)
        {
            _actions.RemoveAction(ent.Comp.ThrowLureActionEntity);
            _actions.AddAction(fisher, ref ent.Comp.PullLureActionEntity, ent.Comp.PullLureActionId, ent);
        }
        else
        {
            _actions.RemoveAction(ent.Comp.PullLureActionEntity);
            _actions.AddAction(fisher, ref ent.Comp.ThrowLureActionEntity, ent.Comp.ThrowLureActionId, ent);
        }
    }

    protected abstract void CalculateFightingTimings(Entity<ActiveFisherComponent> fisher, ActiveFishingSpotComponent activeSpotComp);
    protected abstract void SetupFishingFloat(Entity<FishingRodComponent> fishingRod, EntityUid player, EntityCoordinates target);
    protected abstract void ThrowFishReward(EntProtoId fishId, EntityUid fishSpot, EntityUid target);

    private void StopFishing(Entity<FishingRodComponent> fishingRod, EntityUid? fisher)
    {
        var lureMissing = fishingRod.Comp.FishingLure == null || TerminatingOrDeleted(fishingRod.Comp.FishingLure.Value);

        if (!lureMissing && FishLureQuery.TryComp(fishingRod.Comp.FishingLure, out var lureComp) &&
            !TerminatingOrDeleted(lureComp.AttachedEntity) &&
            ActiveFishSpotQuery.TryComp(lureComp.AttachedEntity, out var activeSpotComp))
            RemCompDeferred(lureComp.AttachedEntity.Value, activeSpotComp);

        if (!lureMissing && Net.IsServer)
            QueueDel(fishingRod.Comp.FishingLure);

        if (Exists(fisher) && !TerminatingOrDeleted(fisher) && FisherQuery.TryComp(fisher, out var fisherComp))
            RemCompDeferred(fisher.Value, fisherComp);

        fishingRod.Comp.FishingLure = null;
        Dirty(fishingRod);
        if (fisher != null)
            ToggleFishingActions(fishingRod, fisher.Value, false);
    }

    private void OnRodTerminating(Entity<FishingRodComponent> ent, ref EntityTerminatingEvent args)
        => TryStopFishing(ent);

    private void OnLureTerminating(Entity<FishingLureComponent> ent, ref EntityTerminatingEvent args)
        => TryStopFishing(ent);

    private void OnSpotTerminating(Entity<ActiveFishingSpotComponent> ent, ref EntityTerminatingEvent args)
        => TryStopFishing(ent);

    private void TryStopFishing(Entity<FishingRodComponent> rod)
        => StopFishing(rod, Transform(rod).ParentUid);

    private void TryStopFishing(Entity<FishingLureComponent> lure)
    {
        if (FishRodQuery.TryComp(lure.Comp.FishingRod, out var rodComp))
            TryStopFishing((lure.Comp.FishingRod, rodComp));
    }

    private void TryStopFishing(Entity<ActiveFishingSpotComponent> spot)
    {
        if (FishLureQuery.TryComp(spot.Comp.AttachedFishingLure, out var lureComp) &&
            FishRodQuery.TryComp(lureComp.FishingRod, out var rodComp))
            TryStopFishing((lureComp.FishingRod, rodComp));
    }

    private void OnThrowFloat(Entity<FishingRodComponent> ent, ref ThrowFishingLureActionEvent args)
    {
        if (args.Handled || !Timing.IsFirstTimePredicted)
            return;

        if (ent.Comp.FishingLure != null || !Xform.IsValid(args.Target))
        {
            args.Handled = true;
            return;
        }

        SetupFishingFloat(ent, args.Performer, args.Target);
        ToggleFishingActions(ent, args.Performer, true);
        args.Handled = true;
    }

    private void OnPullFloat(Entity<FishingRodComponent> ent, ref PullFishingLureActionEvent args)
    {
        if (args.Handled || !Timing.IsFirstTimePredicted)
            return;

        var player = args.Performer;
        if (ent.Comp.FishingLure == null)
        {
            ToggleFishingActions(ent, player, false);
            args.Handled = true;
            return;
        }

        _popup.PopupEntity(Loc.GetString("fishing-rod-remove-lure", ("ent", Name(ent))), ent, ent);

        if (!FishLureQuery.TryComp(ent.Comp.FishingLure, out var lureComp))
        {
            StopFishing(ent, player);
            args.Handled = true;
            return;
        }

        if (lureComp.AttachedEntity != null && Exists(lureComp.AttachedEntity))
        {
            var attachedEnt = lureComp.AttachedEntity.Value;
            var targetCoords = Xform.GetMapCoordinates(Transform(attachedEnt));
            var playerCoords = Xform.GetMapCoordinates(Transform(player));
            var direction = (playerCoords.Position - targetCoords.Position) * _random.NextFloat(0.2f, 0.85f);
            Throwing.TryThrow(attachedEnt, direction, 4f, player);
        }

        StopFishing(ent, player);
        args.Handled = true;
    }

    private void OnFishingRodInit(Entity<FishingRodComponent> ent, ref MapInitEvent args)
        => _actions.AddAction(ent, ref ent.Comp.ThrowLureActionEntity, ent.Comp.ThrowLureActionId);

    private void OnRodParentChanged(Entity<FishingRodComponent> ent, ref EntParentChangedMessage args)
    {
        if (!TerminatingOrDeleted(ent) &&
            Exists(args.Transform.ParentUid) &&
            (!FisherQuery.TryComp(args.Transform.ParentUid, out var fisher) || fisher.FishingRod != ent.Owner))
            StopFishing(ent, args.OldParent);
    }

    private void OnGetActions(Entity<FishingRodComponent> ent, ref GetItemActionsEvent args)
    {
        if (ent.Comp.FishingLure == null)
            args.AddAction(ref ent.Comp.ThrowLureActionEntity, ent.Comp.ThrowLureActionId);
        else
            args.AddAction(ref ent.Comp.PullLureActionEntity, ent.Comp.PullLureActionId);
    }
}
