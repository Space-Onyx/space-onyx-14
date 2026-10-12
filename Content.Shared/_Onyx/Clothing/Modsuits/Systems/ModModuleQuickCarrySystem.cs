// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Item;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleQuickCarrySystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private HeldSpeedModifierSystem _heldSpeed = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModModuleQuickCarryEffectComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
        SubscribeLocalEvent<ModModuleQuickCarryEffectComponent, ComponentRemove>(OnRemoved);
        SubscribeLocalEvent<ModModuleQuickCarryEffectComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<PullableComponent, MobStateChangedEvent>(OnPulledStateChanged);
    }

    private void OnSpeed(Entity<ModModuleQuickCarryEffectComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<PullerComponent>(ent, out var puller) || puller.Pulling is not { } pulled || !_mobState.IsIncapacitated(pulled))
            return;
        var walk = TryComp<PullableComponent>(pulled, out var pullable) ? pullable.PullerSpeedModifier : puller.WalkSpeedModifier;
        var sprint = walk;
        if (TryComp<HeldSpeedModifierComponent>(pulled, out var held))
            (walk, sprint) = _heldSpeed.GetHeldMovementSpeedModifiers(pulled, held);
        args.ModifySpeed(Compensate(walk, ent.Comp.Multiplier), Compensate(sprint, ent.Comp.Multiplier));
    }

    private static float Compensate(float speed, float multiplier) => speed is > 0 and < 1
        ? (1f - (1f - speed) * Math.Clamp(multiplier, 0f, 1f)) / speed
        : 1f;

    private void OnRemoved(Entity<ModModuleQuickCarryEffectComponent> ent, ref ComponentRemove args) =>
        _speed.RefreshMovementSpeedModifiers(ent.Owner);

    private void OnState(Entity<ModModuleQuickCarryEffectComponent> ent, ref AfterAutoHandleStateEvent args) =>
        _speed.RefreshMovementSpeedModifiers(ent.Owner);

    private void OnPulledStateChanged(Entity<PullableComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.Puller is { } puller && HasComp<ModModuleQuickCarryEffectComponent>(puller))
            _speed.RefreshMovementSpeedModifiers(puller);
    }
}
