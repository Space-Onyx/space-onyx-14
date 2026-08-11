// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 ss14-Starlight <ss14-Starlight@outlook.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Onyx.VentCrawling;
using Content.Shared.Eye;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Onyx.VentCrawling;

public sealed partial class VentCrawableSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SharedVentCrawableSystem _ventCrawableSystem = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VentCrawlerHolderComponent, VentCrawlingExitEvent>(OnExit);
        SubscribeLocalEvent<VentCrawlerHolderComponent, ComponentShutdown>(OnHolderShutdown);
    }

    private void OnExit(EntityUid uid, VentCrawlerHolderComponent holder, ref VentCrawlingExitEvent args)
    {
        var holderTransform = args.HolderTransform;
        if (Terminating(uid) || !Resolve(uid, ref holderTransform))
            return;

        if (holder.IsExitingVentCraws)
        {
            Log.Error("Tried exiting vent craws twice.");
            return;
        }

        holder.IsExitingVentCraws = true;
        _ventCrawableSystem.StopCrawlSound(holder);
        var exitCoordinates = holderTransform.Coordinates;
        foreach (var entity in holder.Container.ContainedEntities.ToArray())
        {
            RemComp<BeingVentCrawlerComponent>(entity);
            _containerSystem.Remove(entity, holder.Container, reparent: false, force: true);

            var transform = Transform(entity);
            if (transform.ParentUid != uid)
                continue;

            _transformSystem.SetCoordinates(entity, exitCoordinates);
            _transformSystem.AttachToGridOrMap(entity, transform);
            if (TryComp<VentCrawlerComponent>(entity, out var crawler))
            {
                crawler.InTube = false;
                crawler.VisibleTubes.Clear();
                Dirty(entity, crawler);
                _eye.RefreshVisibilityMask(entity);
            }

            if (TryComp<PhysicsComponent>(entity, out var physics))
            {
                _physicsSystem.SetCanCollide(entity, true, body: physics);
                _physicsSystem.WakeBody(entity, body: physics);
            }
        }

        Del(uid);
    }

    private void OnHolderShutdown(EntityUid uid, VentCrawlerHolderComponent holder, ComponentShutdown args)
    {
        _ventCrawableSystem.StopCrawlSound(holder);
        var coordinates = Transform(uid).Coordinates;
        foreach (var entity in holder.Container.ContainedEntities.ToArray())
        {
            RemComp<BeingVentCrawlerComponent>(entity);
            _containerSystem.Remove(entity, holder.Container, reparent: false, force: true);
            var transform = Transform(entity);
            _transformSystem.SetCoordinates(entity, coordinates);
            _transformSystem.AttachToGridOrMap(entity, transform);
            if (TryComp<VentCrawlerComponent>(entity, out var crawler))
            {
                crawler.InTube = false;
                crawler.VisibleTubes.Clear();
                Dirty(entity, crawler);
                _eye.RefreshVisibilityMask(entity);
            }

            if (TryComp<PhysicsComponent>(entity, out var physics))
            {
                _physicsSystem.SetCanCollide(entity, true, body: physics);
                _physicsSystem.WakeBody(entity, body: physics);
            }
        }
    }
}
