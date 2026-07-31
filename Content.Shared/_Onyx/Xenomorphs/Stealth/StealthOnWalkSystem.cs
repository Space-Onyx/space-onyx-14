using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.Shared._Onyx.Xenomorphs.Stealth;

public sealed partial class StealthOnWalkSystem : EntitySystem
{
    [Dependency] private SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StealthOnWalkComponent, MoveInputEvent>(OnMoveInput);
    }

    private void OnMoveInput(EntityUid uid, StealthOnWalkComponent component, ref MoveInputEvent args)
    {
        if (!TryComp<StealthComponent>(uid, out var stealth) || stealth.Enabled == !args.Entity.Comp.Sprinting)
            return;

        _stealth.SetEnabled(uid, !args.Entity.Comp.Sprinting, stealth);
        component.Stealth = stealth.Enabled;
    }
}
