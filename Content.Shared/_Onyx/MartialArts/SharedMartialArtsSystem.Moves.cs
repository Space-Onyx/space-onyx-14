namespace Content.Shared._Onyx.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private readonly record struct MoveContext(
        EntityUid Performer,
        EntityUid Target,
        ComboPrototype Move,
        CanPerformComboComponent Combo,
        bool Downed,
        float Power);

    private readonly record struct MoveResult(
        bool Success = true,
        bool ApplyDamage = true,
        bool ApplyStamina = true,
        bool ApplySound = true,
        bool ApplyPopup = true)
    {
        public static readonly MoveResult Failed = new(false, false, false, false, false);

        public MoveResult() : this(true, true, true, true, true)
        {
        }
    }

    private void InitializeMoveEvents()
    {
        InitializeJudoMoves();
        InitializeCqcMoves();
        InitializeSleepingCarpMoves();
        InitializeCapoeiraMoves();
        InitializeDragonMoves();
        InitializeNinjutsuMoves();
        InitializeHellRipMoves();
    }

    private MoveResult PerformDefaultMove(MoveContext context) => new();
}
