using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts;

[RegisterComponent]
public sealed partial class MartialArtsPolymorphComponent : Component;

[RegisterComponent]
public sealed partial class MartialArtBlockedComponent : Component
{
    [DataField]
    public MartialArtsForms Form;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MartialArtsKnowledgeComponent : Component
{
    [DataField, AutoNetworkedField] public MartialArtsForms MartialArtsForm;
    [DataField, AutoNetworkedField] public bool Blocked;
    [DataField, AutoNetworkedField] public float DamageBonus;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CanPerformComboComponent : Component
{
    [AutoNetworkedField] public EntityUid? CurrentTarget;
    [AutoNetworkedField] public List<ComboAttackType> LastAttacks = [];
    [DataField] public TimeSpan ResetAfter = TimeSpan.FromSeconds(5);
    [AutoNetworkedField] public TimeSpan ResetAt;
    [AutoNetworkedField] public int ConsecutiveGnashes;
}

public abstract partial class GrantMartialArtKnowledgeComponent : Component
{
    public virtual MartialArtsForms MartialArtsForm => MartialArtsForms.CloseQuartersCombat;
    public virtual LocId? LearnMessage => null;
    [DataField] public bool MultiUse;
    [DataField] public string? SpawnedProto = "Ash";
    [DataField] public SoundSpecifier? SoundOnUse = new SoundPathSpecifier("/Audio/Effects/fire.ogg");
}

[RegisterComponent]
public sealed partial class GrantCqcComponent : GrantMartialArtKnowledgeComponent
{
    [DataField] public bool IsBlocked;
    public override LocId? LearnMessage => "cqc-success-learned";
}

[RegisterComponent]
public sealed partial class GrantCorporateJudoComponent : GrantMartialArtKnowledgeComponent
{
    public override MartialArtsForms MartialArtsForm => MartialArtsForms.CorporateJudo;
}

[RegisterComponent]
public sealed partial class GrantCapoeiraComponent : GrantMartialArtKnowledgeComponent
{
    public override MartialArtsForms MartialArtsForm => MartialArtsForms.Capoeira;
    public override LocId? LearnMessage => "capoeira-success-learned";
}

[RegisterComponent]
public sealed partial class GrantKungFuDragonComponent : GrantMartialArtKnowledgeComponent
{
    public override MartialArtsForms MartialArtsForm => MartialArtsForms.KungFuDragon;
    public override LocId? LearnMessage => "dragon-success-learned";
}

[RegisterComponent]
public sealed partial class GrantNinjutsuComponent : GrantMartialArtKnowledgeComponent
{
    public override MartialArtsForms MartialArtsForm => MartialArtsForms.Ninjutsu;
    public override LocId? LearnMessage => "ninjutsu-success-learned";
}

[RegisterComponent]
public sealed partial class GrantSleepingCarpComponent : GrantMartialArtKnowledgeComponent
{
    public override MartialArtsForms MartialArtsForm => MartialArtsForms.SleepingCarp;
    [DataField] public int MaximumUses = 1;
    public int CurrentUses;
}

[RegisterComponent]
public sealed partial class SleepingCarpStudentComponent : Component
{
    [DataField] public int Stage = 1;
    public TimeSpan UseAgainTime;
    [DataField] public int MaxUseDelay = 90;
    [DataField] public int MinUseDelay = 30;
}

[RegisterComponent]
public sealed partial class SleepingCarpEffectsComponent : Component
{
    public bool AddedReflect;
    public bool AddedDragonFaction;
    public float OriginalReflectProbability;
    public Angle OriginalReflectSpread;
}

[RegisterComponent]
public sealed partial class GrantHellRipComponent : GrantMartialArtKnowledgeComponent
{
    public override MartialArtsForms MartialArtsForm => MartialArtsForms.HellRip;
    public override LocId? LearnMessage => "hellrip-success-learned";
}

[RegisterComponent]
public sealed partial class ArmbarredComponent : Component
{
    public EntityUid Puller;
}

[RegisterComponent]
public sealed partial class CorporateJudoGrantSourcesComponent : Component
{
    public int Count;
    public bool GrantedArt;
}

[RegisterComponent]
public sealed partial class NinjutsuSneakAttackComponent : Component
{
    public TimeSpan SurpriseReadyAt;
    [DataField] public float Multiplier = 2f;
}

[RegisterComponent]
public sealed partial class DragonKungFuComponent : Component
{
    public TimeSpan LastMoveTime;
    [DataField] public float MinVelocity = 0.2f;
    [DataField] public TimeSpan PauseDuration = TimeSpan.FromSeconds(2);
    public bool PowerReady;
}

[RegisterComponent]
public sealed partial class MartialArtModifiersComponent : Component
{
    public float AttackRate = 1f;
    public float Damage = 1f;
    public float MoveSpeed = 1f;
    public TimeSpan AttackRateUntil;
    public TimeSpan DamageUntil;
    public TimeSpan MoveSpeedUntil;
}

[RegisterComponent]
public sealed partial class KravMagaComponent : Component
{
    public bool Enabled;
    public KravMagaMoves? SelectedMove;
    public float SelectedStaminaDamage;
    public float SelectedEffectTime;
    public readonly List<EntityUid> Actions = [];
    [DataField] public int BaseDamage = 5;
    [DataField] public int DownedDamageModifier = 2;
}

[RegisterComponent]
public sealed partial class KravMagaActionComponent : Component
{
    [DataField] public KravMagaMoves Configuration;
    [DataField] public float StaminaDamage;
    [DataField] public float EffectTime;
}

[RegisterComponent]
public sealed partial class KravMagaSilencedComponent : Component
{
    public TimeSpan Until;
}

[RegisterComponent]
public sealed partial class KravMagaBlockedBreathingComponent : Component
{
    public TimeSpan Until;
}
