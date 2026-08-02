using Robust.Shared.Random;

namespace Content.Shared._Onyx.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeSleepingCarpMoves()
    {
        SubscribeLocalEvent<CanPerformComboComponent, CarpGnashingTeethEvent>((Entity<CanPerformComboComponent> ent, ref CarpGnashingTeethEvent args) => PerformMove(ent, PerformCarpGnashingTeeth));
        SubscribeLocalEvent<CanPerformComboComponent, CarpKneeHaulEvent>((Entity<CanPerformComboComponent> ent, ref CarpKneeHaulEvent args) => PerformMove(ent, PerformCarpKneeHaul));
        SubscribeLocalEvent<CanPerformComboComponent, CarpCrashingWavesEvent>((Entity<CanPerformComboComponent> ent, ref CarpCrashingWavesEvent args) => PerformMove(ent, PerformPushMove));
    }

    private MoveResult PerformCarpGnashingTeeth(MoveContext context)
    {
        Damage(context.Target, context.Performer, context.Move.DamageType, context.Move.ExtraDamage + context.Combo.ConsecutiveGnashes++ * 5);
        if (_prototypes.TryIndex<MartialArtPrototype>(MartialArtsForms.SleepingCarp.ToString(), out var carp))
        {
            var sayings = context.Downed ? carp.RandomSayingsDowned : carp.RandomSayings;
            if (sayings.Count > 0)
                RaiseLocalEvent(context.Performer, new SleepingCarpSaying(_random.Pick(sayings)));
        }
        return new(ApplyDamage: false);
    }

    private MoveResult PerformCarpKneeHaul(MoveContext context)
    {
        if (!context.Downed)
        {
            Knockdown(context.Target, context.Move);
            Damage(context.Target, context.Performer, context.Move.DamageType, context.Move.ExtraDamage);
            _stamina.TakeStaminaDamage(context.Target, context.Move.StaminaDamage, source: context.Performer);
        }
        else
        {
            _hands.TryDrop(context.Target);
            Damage(context.Target, context.Performer, context.Move.DamageType, context.Move.ExtraDamage / 2f);
            _stamina.TakeStaminaDamage(context.Target, context.Move.StaminaDamage - 20f, source: context.Performer);
        }
        StopPull(context.Target, context.Performer);
        return new(ApplyDamage: false, ApplyStamina: false);
    }

    private MoveResult PerformPushMove(MoveContext context)
    {
        if (context.Downed)
            return MoveResult.Failed;
        Knockdown(context.Target, context.Move, context.Power);
        StopPull(context.Target, context.Performer);
        ThrowAway(context.Performer, context.Target, context.Move.ThrownSpeed * context.Power);
        return new();
    }
}
