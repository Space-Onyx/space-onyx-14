using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Xenomorphs.FaceHugger;

[RegisterComponent]
public sealed partial class FaceHuggerComponent : Component
{
    [DataField]
    public (BodyPartType Type, BodyPartSymmetry Symmetry) InfectionBodyPart =
        (BodyPartType.Chest, BodyPartSymmetry.None);

    [DataField]
    public DamageSpecifier DamageOnImpact = new();

    [DataField]
    public DamageSpecifier DamageOnInfect = new();

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public EntProtoId? InfectionPrototype = "XenomorphInfection";

    [DataField]
    public string InfectionSlotId = "xenomorph_larva";

    [DataField]
    public string Slot = "mask";

    [DataField]
    public SoundSpecifier SoundOnImpact = new SoundCollectionSpecifier("MetalThud");

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MaxInfectTime = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan MinInfectTime = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan MaxRestTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MinRestTime = TimeSpan.FromSeconds(3);

    [DataField]
    public string SleepChem = "Nocturine";

    [DataField]
    public float SleepChemAmount = 10f;

    [DataField]
    public float MinChemicalThreshold;

    [DataField]
    public TimeSpan InjectionInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan InitialInjectionDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan AttachAttemptDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public DamageSpecifier MaskBlockDamage = new();

    [DataField]
    public SoundSpecifier MaskBlockSound = new SoundCollectionSpecifier("MetalThud");

    [ViewVariables]
    public bool Active = true;

    [ViewVariables]
    public TimeSpan RestIn;

    [ViewVariables]
    public TimeSpan InfectIn;

    [ViewVariables]
    public TimeSpan NextInjectionTime;
}
