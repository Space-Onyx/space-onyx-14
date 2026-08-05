using System.Numerics;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Throwing;

namespace Content.Shared._Onyx.Moth;

public sealed partial class WingDashSystem : EntitySystem
{
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WingDashActionEvent>(OnDash);
    }

    private void OnDash(WingDashActionEvent args)
    {
        if (args.Handled || _gravity.IsWeightless(args.Performer))
            return;

        var direction = _transform.ToMapCoordinates(args.Target).Position -
                        _transform.GetMapCoordinates(args.Performer).Position;
        if (direction == Vector2.Zero)
            return;

        var distance = args.Distance;
        var speed = args.Speed;
        if (TryComp(args.Performer, out MovementSpeedModifierComponent? movement) && movement.BaseSprintSpeed > 0f)
        {
            var modifier = movement.CurrentSprintSpeed / movement.BaseSprintSpeed;
            distance *= modifier;
            speed *= modifier;
        }

        args.Handled = true;
        _throwing.TryThrow(args.Performer, direction.Normalized() * distance, speed, animated: true);
        _stamina.TakeStaminaDamage(args.Performer, args.StaminaDrain, visual: false);
    }
}
