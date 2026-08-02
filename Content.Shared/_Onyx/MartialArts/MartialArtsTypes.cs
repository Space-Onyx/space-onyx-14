using Content.Shared.Actions;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.MartialArts;

[Prototype]
public sealed partial class MartialArtPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public MartialArtsForms MartialArtsForm;
    [DataField(required: true)] public ProtoId<ComboListPrototype> RoundstartCombos;
    [DataField] public float BaseDamageModifier;
    [DataField] public string DamageModifierType = "Blunt";
    [DataField] public bool RandomDamageModifier;
    [DataField] public int MinRandomDamageModifier;
    [DataField] public int MaxRandomDamageModifier = 5;
    [DataField] public List<LocId> RandomSayings = [];
    [DataField] public List<LocId> RandomSayingsDowned = [];
}

[Prototype]
public sealed partial class ComboPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public MartialArtsForms MartialArtsForm;
    [DataField("attacks", required: true)] public List<ComboAttackType> AttackTypes = [];
    [DataField(required: true)] public string Name = string.Empty;
    [DataField("event", required: true)] public object? ResultEvent;
    [DataField] public float ExtraDamage;
    [DataField] public float StaminaDamage;
    [DataField] public float ParalyzeTime;
    [DataField] public bool DropItems;
    [DataField] public bool CanDoWhileProne = true;
    [DataField] public bool PerformOnSelf;
    [DataField] public float ThrownSpeed = 7f;
    [DataField] public string DamageType = "Blunt";
    [DataField] public float MinVelocity;
    [DataField] public float StaminaToHeal;
    [DataField] public float AttackSpeedMultiplier = 1f;
    [DataField] public float AttackSpeedMultiplierTime;
    [DataField] public SoundSpecifier? Sound;
}

[Prototype]
public sealed partial class ComboListPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public List<ProtoId<ComboPrototype>> Combos = [];
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
}

[Serializable, NetSerializable]
public enum ComboAttackType : byte
{
    Harm,
    HarmLight,
    Disarm,
    Grab,
}

public sealed class ComboAttackPerformedEvent(EntityUid performer, EntityUid target, EntityUid weapon, ComboAttackType type)
    : CancellableEntityEventArgs
{
    public EntityUid Performer { get; } = performer;
    public EntityUid Target { get; } = target;
    public EntityUid Weapon { get; } = weapon;
    public ComboAttackType Type { get; } = type;
}

[ByRefEvent]
public record struct GetPerformedAttackTypesEvent(List<ComboAttackType>? AttackTypes = null);

public sealed class MartialArtsPolymorphCopyEvent(EntityUid destination) : EntityEventArgs
{
    public EntityUid Destination { get; } = destination;
}

[ByRefEvent]
public record struct GetMeleeTargetModifiersEvent(List<DamageModifierSet> Modifiers);

public sealed class SleepingCarpSaying(LocId saying) : EntityEventArgs
{
    public LocId Saying { get; } = saying;
}

public sealed partial class KravMagaActionEvent : InstantActionEvent;

public enum KravMagaMoves : byte
{
    LegSweep,
    NeckChop,
    LungPunch,
}
