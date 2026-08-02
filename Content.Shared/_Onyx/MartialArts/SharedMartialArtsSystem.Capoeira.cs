namespace Content.Shared._Onyx.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeCapoeiraMoves()
    {
        SubscribeLocalEvent<CanPerformComboComponent, PushKickEvent>((Entity<CanPerformComboComponent> ent, ref PushKickEvent args) => PerformMove(ent, PerformPushKick));
        SubscribeLocalEvent<CanPerformComboComponent, CircleKickEvent>((Entity<CanPerformComboComponent> ent, ref CircleKickEvent args) => PerformMove(ent, PerformCircleKick));
        SubscribeLocalEvent<CanPerformComboComponent, SweepKickEvent>((Entity<CanPerformComboComponent> ent, ref SweepKickEvent args) => PerformMove(ent, PerformSweepKick));
        SubscribeLocalEvent<CanPerformComboComponent, SpinKickEvent>((Entity<CanPerformComboComponent> ent, ref SpinKickEvent args) => PerformMove(ent, PerformSpinKick));
        SubscribeLocalEvent<CanPerformComboComponent, KickUpEvent>((Entity<CanPerformComboComponent> ent, ref KickUpEvent args) => PerformMove(ent, PerformKickUp));
    }

    private MoveResult PerformPushKick(MoveContext context)
    {
        var result = PerformPushMove(context);
        if (result.Success)
            _statusEffects.TryUpdateStatusEffectDuration(context.Target, "StatusEffectMeleeVulnerability", TimeSpan.FromSeconds(context.Move.ParalyzeTime * context.Power));
        return result;
    }

    private MoveResult PerformCircleKick(MoveContext context)
    {
        _movement.TryUpdateMovementSpeedModDuration(context.Target, SlowdownEffect, TimeSpan.FromSeconds(2 * context.Power), 1f / context.Power);
        return new();
    }

    private MoveResult PerformSweepKick(MoveContext context)
    {
        Knockdown(context.Target, context.Move, context.Power);
        return new();
    }

    private MoveResult PerformSpinKick(MoveContext context)
    {
        if (context.Downed)
            return MoveResult.Failed;
        Knockdown(context.Target, context.Move, context.Power);
        StopPull(context.Target, context.Performer);
        return new();
    }

    private MoveResult PerformKickUp(MoveContext context)
    {
        _standing.Stand(context.Performer);
        return new();
    }
}
