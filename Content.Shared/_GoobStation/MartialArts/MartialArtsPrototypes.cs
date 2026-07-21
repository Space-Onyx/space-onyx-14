using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts;

[Prototype]
public sealed partial class ComboPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public MartialArtsForms MartialArtsForm;
    [DataField("attacks", required: true)] public List<ComboAttackType> AttackTypes = new();
    [DataField(required: true)] public string Name = string.Empty;
    [DataField] public float ExtraDamage;
    [DataField] public float StaminaDamage;
    [DataField] public float ParalyzeTime;
    [DataField] public bool DropItems;
    [DataField] public bool CanDoWhileProne = true;
    [DataField] public bool PerformOnSelf;
    [DataField] public bool ThrowTarget;
    [DataField] public float ThrownSpeed = 7f;
    [DataField] public string DamageType = "Blunt";
    [DataField] public float SilenceTime;
    [DataField] public float BlockBreathingTime;
}

[Prototype]
public sealed partial class ComboListPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public List<ProtoId<ComboPrototype>> Combos = new();
}

[Prototype]
public sealed partial class MartialArtPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public MartialArtsForms MartialArtsForm;
    [DataField(required: true)] public ProtoId<ComboListPrototype> RoundstartCombos;
}

public enum MartialArtsForms : byte
{
    CorporateJudo,
    CloseQuartersCombat,
    SleepingCarp,
    Capoeira,
    KungFuDragon,
    Ninjutsu,
    HellRip,
    KravMaga,
}
