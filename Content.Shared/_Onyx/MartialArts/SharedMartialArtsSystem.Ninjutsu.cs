namespace Content.Shared._Onyx.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeNinjutsuMoves()
    {
        SubscribeLocalEvent<CanPerformComboComponent, BiteTheDustEvent>((Entity<CanPerformComboComponent> ent, ref BiteTheDustEvent args) => PerformMove(ent, PerformBiteTheDust));
        SubscribeLocalEvent<CanPerformComboComponent, DirtyKillEvent>((Entity<CanPerformComboComponent> ent, ref DirtyKillEvent args) => PerformMove(ent, PerformDirtyKill));
    }

    private MoveResult PerformBiteTheDust(MoveContext context)
    {
        if (context.Downed)
            return MoveResult.Failed;
        Knockdown(context.Target, context.Move);
        return new();
    }

    private MoveResult PerformDirtyKill(MoveContext context)
    {
        if (!context.Downed)
            return MoveResult.Failed;
        _stun.TryUpdateStunDuration(context.Target, TimeSpan.FromSeconds(context.Move.ParalyzeTime));
        return new();
    }
}
