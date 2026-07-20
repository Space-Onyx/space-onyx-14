using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Onyx.Carrying;

public sealed partial class CarryingSlowdownSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CarryingSlowdownComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public void SetModifier(Entity<CarryingSlowdownComponent?> entity, float modifier)
    {
        entity.Comp ??= EnsureComp<CarryingSlowdownComponent>(entity);
        entity.Comp.Modifier = modifier;
        Dirty(entity, entity.Comp);
        _movementSpeed.RefreshMovementSpeedModifiers(entity);
    }

    private static void OnRefreshSpeed(Entity<CarryingSlowdownComponent> entity, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(entity.Comp.Modifier, entity.Comp.Modifier);
    }
}
