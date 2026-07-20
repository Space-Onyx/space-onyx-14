using Content.Shared._Onyx.Swimming.Components;
using Content.Shared._Onyx.Swimming.Events;
using Content.Shared._Onyx.Swimming.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Server.Body.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Swimming;

public sealed partial class OceanSwimmingStaminaSystem : EntitySystem
{
    private static readonly TimeSpan StaminaSuppressLeadTime = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StaminaSuppressRefreshThreshold = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan DrowningDamageInterval = TimeSpan.FromSeconds(1);
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationDamageType = "Asphyxiation";

    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;

    private DamageTypePrototype _asphyxiation = default!;

    public override void Initialize()
    {
        base.Initialize();

        _asphyxiation = _prototype.Index(AsphyxiationDamageType);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var suppressUntil = now + StaminaSuppressLeadTime;
        var refreshSuppressAt = now + StaminaSuppressRefreshThreshold;

        var query = EntityQueryEnumerator<
            OceanSwimmingComponent,
            InputMoverComponent,
            StaminaComponent,
            TransformComponent>();

        while (query.MoveNext(out var uid, out var swimming, out var mover, out var stamina, out var xform))
        {
            var isSwimming = xform.GridUid == null && xform.ParentUid == xform.MapUid;

            if (xform.MapUid is not { } mapUid ||
                !TryComp<OceanMapComponent>(mapUid, out var ocean) || !isSwimming)
            {
                RemComp<OceanSwimmingComponent>(uid);
                continue;
            }

            var canSwim = OceanSwimmingStamina.CanSwim(swimming, stamina, ocean);
            var moving = mover.HasDirectionalMovement && mover.CanMove && canSwim;

            var staminaCost = MathF.Max(0f, ocean.StaminaCost);
            var staminaRecovery = MathF.Max(0f, ocean.StaminaRecovery);

            if (TryComp<SwimmingModifierComponent>(uid, out var modifier))
                staminaCost *= MathF.Max(0f, modifier.StaminaCostMultiplier);

            var sprint = new OceanSwimmingSprintEvent();
            RaiseLocalEvent(uid, sprint);

            if (moving)
            {
                if (sprint.IsSprinting)
                    staminaCost *= MathF.Max(1f, ocean.SprintStaminaCostMultiplier);

                if (staminaCost > 0f)
                {
                    _stamina.TakeStaminaDamage(
                        uid,
                        staminaCost * frameTime,
                        stamina,
                        source: uid,
                        visual: false,
                        ignoreResist: false);
                }
            }
            else if (stamina.StaminaDamage > 0f && staminaRecovery > 0f)
            {
                if (sprint.IsSprinting)
                    staminaRecovery /= MathF.Max(0.01f, sprint.StaminaRegenMultiplier);

                _stamina.TakeStaminaDamage(
                    uid,
                    -staminaRecovery * frameTime,
                    stamina,
                    source: uid,
                    visual: false,
                    ignoreResist: false);
            }

            ApplyDrowning(uid, swimming, stamina, ocean, now);

            if (stamina.NextUpdate >= refreshSuppressAt)
                continue;

            stamina.NextUpdate = suppressUntil;
            Dirty(uid, stamina);
        }
    }

    private void ApplyDrowning(
        EntityUid uid,
        OceanSwimmingComponent swimming,
        StaminaComponent stamina,
        OceanMapComponent ocean,
        TimeSpan now)
    {
        if (!HasComp<RespiratorComponent>(uid) ||
            stamina.CritThreshold <= 0f)
        {
            return;
        }

        var staminaRemainingFraction = Math.Clamp(
            (stamina.CritThreshold - stamina.StaminaDamage) / stamina.CritThreshold,
            0f,
            1f);

        if (staminaRemainingFraction > Math.Clamp(ocean.DrowningStaminaThreshold, 0f, 1f))
        {
            swimming.NextDrowningDamage = TimeSpan.Zero;
            return;
        }

        if (now < swimming.NextDrowningDamage)
            return;

        swimming.NextDrowningDamage = now + DrowningDamageInterval;

        var damage = MathF.Max(0f, ocean.DrowningDamage) *
                     (float) DrowningDamageInterval.TotalSeconds;
        if (damage <= 0f)
            return;

        _damageable.TryChangeDamage(
            uid,
            new DamageSpecifier(_asphyxiation, damage),
            interruptsDoAfters: false,
            origin: uid);
    }
}
