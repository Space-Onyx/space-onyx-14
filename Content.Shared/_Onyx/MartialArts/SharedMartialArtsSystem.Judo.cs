using Content.Goobstation.Shared.GrabIntent;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Movement.Pulling.Components;

namespace Content.Shared._Onyx.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeJudoMoves()
    {
        SubscribeLocalEvent<CanPerformComboComponent, JudoDiscombobulateEvent>((Entity<CanPerformComboComponent> ent, ref JudoDiscombobulateEvent args) => PerformMove(ent, PerformJudoDiscombobulate));
        SubscribeLocalEvent<CanPerformComboComponent, JudoEyePokeEvent>((Entity<CanPerformComboComponent> ent, ref JudoEyePokeEvent args) => PerformMove(ent, PerformJudoEyePoke));
        SubscribeLocalEvent<CanPerformComboComponent, JudoThrowEvent>((Entity<CanPerformComboComponent> ent, ref JudoThrowEvent args) => PerformMove(ent, PerformJudoThrow));
        SubscribeLocalEvent<CanPerformComboComponent, JudoArmbarEvent>((Entity<CanPerformComboComponent> ent, ref JudoArmbarEvent args) => PerformMove(ent, PerformJudoArmbar));
        SubscribeLocalEvent<CanPerformComboComponent, JudoWheelThrowEvent>((Entity<CanPerformComboComponent> ent, ref JudoWheelThrowEvent args) => PerformMove(ent, PerformJudoWheelThrow));
    }

    private MoveResult PerformJudoDiscombobulate(MoveContext context)
    {
        _movement.TryUpdateMovementSpeedModDuration(context.Target, SlowdownEffect, TimeSpan.FromSeconds(5), 0.5f);
        return new();
    }

    private MoveResult PerformJudoEyePoke(MoveContext context)
    {
        _statusEffects.TryUpdateStatusEffectDuration(context.Target, BlindnessSystem.BlindingStatusEffect, TimeSpan.FromSeconds(2));
        _statusEffects.TryUpdateStatusEffectDuration(context.Target, "StatusEffectMartialArtsBlurryVision", TimeSpan.FromSeconds(5));
        return new();
    }

    private MoveResult PerformJudoThrow(MoveContext context)
    {
        if (context.Downed)
            return MoveResult.Failed;
        Knockdown(context.Target, context.Move, GetStaminaResistance(context.Target));
        StopPull(context.Target, context.Performer);
        return new();
    }

    private MoveResult PerformJudoArmbar(MoveContext context)
    {
        if (!context.Downed || !TryComp<PullerComponent>(context.Performer, out var puller) || puller.Pulling != context.Target)
            return MoveResult.Failed;
        var newArmbar = !TryComp<ArmbarredComponent>(context.Target, out var armbarred);
        armbarred ??= EnsureComp<ArmbarredComponent>(context.Target);
        armbarred.Puller = context.Performer;
        if (TryComp<GrabIntentComponent>(context.Performer, out var grabber)
            && TryComp<GrabbableComponent>(context.Target, out var grabbable))
        {
            grabber.GrabStage = GrabStage.Suffocate;
            grabbable.GrabStage = GrabStage.Suffocate;
            Dirty(context.Performer, grabber);
            Dirty(context.Target, grabbable);
        }
        Knockdown(context.Target, context.Move, GetStaminaResistance(context.Target));
        if (newArmbar)
            _stamina.TakeStaminaDamage(context.Target, context.Move.StaminaDamage, source: context.Performer);
        return new(ApplyStamina: false);
    }

    private MoveResult PerformJudoWheelThrow(MoveContext context)
    {
        if (!context.Downed || !TryComp<ArmbarredComponent>(context.Target, out var armbar) || armbar.Puller != context.Performer)
            return MoveResult.Failed;
        StopPull(context.Target, context.Performer);
        ThrowAway(context.Performer, context.Target, 5f);
        _standing.Stand(context.Performer);
        return new();
    }
}
