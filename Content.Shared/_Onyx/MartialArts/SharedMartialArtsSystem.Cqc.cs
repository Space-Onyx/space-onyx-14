using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Components;

namespace Content.Shared._Onyx.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeCqcMoves()
    {
        SubscribeLocalEvent<CanPerformComboComponent, CqcSlamEvent>((Entity<CanPerformComboComponent> ent, ref CqcSlamEvent args) => PerformMove(ent, PerformCqcSlam));
        SubscribeLocalEvent<CanPerformComboComponent, CqcKickEvent>((Entity<CanPerformComboComponent> ent, ref CqcKickEvent args) => PerformMove(ent, PerformCqcKick));
        SubscribeLocalEvent<CanPerformComboComponent, CqcRestrainEvent>((Entity<CanPerformComboComponent> ent, ref CqcRestrainEvent args) => PerformMove(ent, PerformCqcRestrain));
        SubscribeLocalEvent<CanPerformComboComponent, CqcPressureEvent>((Entity<CanPerformComboComponent> ent, ref CqcPressureEvent args) => PerformMove(ent, PerformCqcPressure));
        SubscribeLocalEvent<CanPerformComboComponent, CqcConsecutiveEvent>((Entity<CanPerformComboComponent> ent, ref CqcConsecutiveEvent args) => PerformMove(ent, PerformDefaultMove));
    }

    private MoveResult PerformCqcSlam(MoveContext context)
    {
        if (context.Downed)
            return MoveResult.Failed;
        Knockdown(context.Target, context.Move);
        StopPull(context.Target, context.Performer);
        return new();
    }

    private MoveResult PerformCqcKick(MoveContext context)
    {
        if (context.Downed)
        {
            Damage(context.Target, context.Performer, context.Move.DamageType, context.Move.ExtraDamage);
            _stamina.TakeStaminaDamage(context.Target, context.Move.StaminaDamage * 2f + 5f, source: context.Performer);
            if (TryComp<StaminaComponent>(context.Target, out var stamina) && stamina.Critical)
                _statusEffects.TryUpdateStatusEffectDuration(context.Target, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(10));
        }
        StopPull(context.Target, context.Performer);
        ThrowAway(context.Performer, context.Target, context.Move.ThrownSpeed);
        return new(ApplyDamage: false, ApplyStamina: !context.Downed);
    }

    private MoveResult PerformCqcRestrain(MoveContext context)
    {
        Knockdown(context.Target, context.Move);
        return new();
    }

    private MoveResult PerformCqcPressure(MoveContext context)
    {
        StealActiveItem(context.Performer, context.Target);
        return new();
    }
}
