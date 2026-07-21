// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <aviu00@protonmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared._Onyx.Fishing.Components;
using Content.Shared._Onyx.Fishing.Systems;
using Content.Shared.EntityTable;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Fishing;

public sealed partial class FishingSystem : SharedFishingSystem
{
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private PhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishingLureComponent, StartCollideEvent>(OnLureCollide);
        SubscribeLocalEvent<FishingRodComponent, UseInHandEvent>(OnFishingInteract);
    }

    private void OnLureCollide(Entity<FishingLureComponent> ent, ref StartCollideEvent args)
    {
        var attachedEnt = args.OtherEntity;
        if (HasComp<ActiveFishingSpotComponent>(attachedEnt))
            return;

        if (!FishSpotQuery.TryComp(attachedEnt, out var spotComp))
        {
            if (args.OtherBody.BodyType != BodyType.Static)
                Anchor(ent, attachedEnt);
            return;
        }

        var fish = _entityTable.GetSpawns(spotComp.FishList).FirstOrDefault();
        if (fish == default)
            return;

        Anchor(ent, attachedEnt);
        _prototype.Index(fish).TryComp(out FishComponent? fishComp, _compFactory);

        var activeSpot = EnsureComp<ActiveFishingSpotComponent>(attachedEnt);
        activeSpot.Fish = fish;
        activeSpot.FishDifficulty = fishComp?.FishDifficulty ?? FishComponent.DefaultDifficulty;
        var wait = spotComp.FishDefaultTimer + _random.NextFloat(-spotComp.FishTimerVariety, spotComp.FishTimerVariety);
        activeSpot.FishingStartTime = Timing.CurTime + TimeSpan.FromSeconds(wait);
        activeSpot.AttachedFishingLure = ent;
        Dirty(attachedEnt, activeSpot);
        Dirty(ent);
    }

    private void OnFishingInteract(EntityUid uid, FishingRodComponent component, UseInHandEvent args)
    {
        if (!FisherQuery.TryComp(args.User, out var fisherComp) || fisherComp.TotalProgress == null ||
            args.Handled || !Timing.IsFirstTimePredicted)
            return;

        fisherComp.TotalProgress += fisherComp.ProgressPerUse * component.Efficiency;
        Dirty(args.User, fisherComp);
        args.Handled = true;
    }

    private void Anchor(Entity<FishingLureComponent> ent, EntityUid attachedEnt)
    {
        Xform.SetWorldPosition(ent, Xform.GetWorldPosition(attachedEnt));
        Xform.SetParent(ent, attachedEnt);
        _physics.SetLinearVelocity(ent, Vector2.Zero);
        _physics.SetAngularVelocity(ent, 0f);
        ent.Comp.AttachedEntity = attachedEnt;
        RemComp<ItemComponent>(ent);
        RemComp<PullableComponent>(ent);
    }

    protected override void SetupFishingFloat(Entity<FishingRodComponent> fishingRod, EntityUid player, EntityCoordinates target)
    {
        var targetCoords = Xform.ToMapCoordinates(target);
        var playerCoords = Xform.GetMapCoordinates(Transform(player));
        var lure = Spawn(fishingRod.Comp.FloatPrototype, playerCoords);
        fishingRod.Comp.FishingLure = lure;
        Dirty(fishingRod);

        var direction = targetCoords.Position - playerCoords.Position;
        if (direction == Vector2.Zero)
            direction = Vector2.UnitX;
        Throwing.TryThrow(lure, direction, 15f, player, 2f, null, true);

        var lureComp = EnsureComp<FishingLureComponent>(lure);
        lureComp.FishingRod = fishingRod;
        Dirty(lure, lureComp);

        var visuals = EnsureComp<JointVisualsComponent>(lure);
        visuals.Sprite = fishingRod.Comp.RopeSprite;
        visuals.OffsetA = fishingRod.Comp.RopeLureOffset;
        visuals.OffsetB = fishingRod.Comp.RopeUserOffset;
        visuals.Target = fishingRod;
        Dirty(lure, visuals);
    }

    protected override void ThrowFishReward(EntProtoId fishId, EntityUid fishSpot, EntityUid target)
    {
        var fish = Spawn(fishId, Transform(fishSpot).Coordinates);
        var direction = Xform.GetWorldPosition(target) - Xform.GetWorldPosition(fish);
        var length = direction.Length();
        if (length == 0f)
            direction = Vector2.UnitX * 0.5f;
        else
            direction *= Math.Clamp(length, 0.5f, 15f) / length;

        Throwing.TryThrow(fish, direction, 7f);
    }

    protected override void CalculateFightingTimings(Entity<ActiveFisherComponent> fisher, ActiveFishingSpotComponent activeSpotComp)
    {
        if (Timing.CurTime < fisher.Comp.NextStruggle)
            return;

        fisher.Comp.NextStruggle = Timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0.06f, 0.18f));
        fisher.Comp.TotalProgress -= activeSpotComp.FishDifficulty;
        Dirty(fisher);
    }
}
