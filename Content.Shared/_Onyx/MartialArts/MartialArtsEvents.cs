namespace Content.Shared._Onyx.MartialArts;

[ImplicitDataDefinitionForInheritors]
public abstract partial class MartialArtMoveEvent : EntityEventArgs;

public sealed partial class JudoDiscombobulateEvent : MartialArtMoveEvent;
public sealed partial class JudoEyePokeEvent : MartialArtMoveEvent;
public sealed partial class JudoThrowEvent : MartialArtMoveEvent;
public sealed partial class JudoArmbarEvent : MartialArtMoveEvent;
public sealed partial class JudoWheelThrowEvent : MartialArtMoveEvent;
public sealed partial class CqcSlamEvent : MartialArtMoveEvent;
public sealed partial class CqcKickEvent : MartialArtMoveEvent;
public sealed partial class CqcRestrainEvent : MartialArtMoveEvent;
public sealed partial class CqcPressureEvent : MartialArtMoveEvent;
public sealed partial class CqcConsecutiveEvent : MartialArtMoveEvent;
public sealed partial class CarpGnashingTeethEvent : MartialArtMoveEvent;
public sealed partial class CarpKneeHaulEvent : MartialArtMoveEvent;
public sealed partial class CarpCrashingWavesEvent : MartialArtMoveEvent;
public sealed partial class PushKickEvent : MartialArtMoveEvent;
public sealed partial class CircleKickEvent : MartialArtMoveEvent;
public sealed partial class SweepKickEvent : MartialArtMoveEvent;
public sealed partial class SpinKickEvent : MartialArtMoveEvent;
public sealed partial class KickUpEvent : MartialArtMoveEvent;
public sealed partial class DragonClawEvent : MartialArtMoveEvent;
public sealed partial class DragonTailEvent : MartialArtMoveEvent;
public sealed partial class DragonStrikeEvent : MartialArtMoveEvent;
public sealed partial class BiteTheDustEvent : MartialArtMoveEvent;
public sealed partial class DirtyKillEvent : MartialArtMoveEvent;
public sealed partial class HellRipDropKickEvent : MartialArtMoveEvent;
public sealed partial class HellRipHeadRipEvent : MartialArtMoveEvent;
public sealed partial class HellRipTearDownEvent : MartialArtMoveEvent;
public sealed partial class HellRipSlamEvent : MartialArtMoveEvent;
