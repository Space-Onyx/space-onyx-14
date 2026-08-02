namespace Content.Shared._Onyx.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeDragonMoves()
    {
        SubscribeLocalEvent<CanPerformComboComponent, DragonClawEvent>((Entity<CanPerformComboComponent> ent, ref DragonClawEvent args) => PerformMove(ent, PerformDragonClaw));
        SubscribeLocalEvent<CanPerformComboComponent, DragonTailEvent>((Entity<CanPerformComboComponent> ent, ref DragonTailEvent args) => PerformMove(ent, PerformDragonTail));
        SubscribeLocalEvent<CanPerformComboComponent, DragonStrikeEvent>((Entity<CanPerformComboComponent> ent, ref DragonStrikeEvent args) => PerformMove(ent, PerformDragonStrike));
    }

    private MoveResult PerformDragonClaw(MoveContext context)
    {
        _movement.TryUpdateMovementSpeedModDuration(context.Target, SlowdownEffect, TimeSpan.FromSeconds(2), 0.6f);
        return new();
    }

    private MoveResult PerformDragonTail(MoveContext context)
    {
        if (context.Downed)
        {
            _stun.TryUpdateStunDuration(context.Target, TimeSpan.FromSeconds(1));
            StopPull(context.Target, context.Performer);
            return new(ApplyDamage: false, ApplyStamina: false);
        }
        Knockdown(context.Target, context.Move);
        StopPull(context.Target, context.Performer);
        return new();
    }

    private MoveResult PerformDragonStrike(MoveContext context)
    {
        if (!context.Downed)
            return MoveResult.Failed;
        _stun.TryUpdateParalyzeDuration(context.Target, TimeSpan.FromSeconds(context.Move.ParalyzeTime));
        return new();
    }
}
