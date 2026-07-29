using Content.Shared._Onyx.StationEvents;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Onyx.StationEvents.SecretPlus;

[RegisterComponent, AutoGenerateComponentPause, Access(typeof(SecretPlusSystem))]
public sealed partial class SecretPlusComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan TimeNextEvent;

    [DataField]
    public TimeSpan EventIntervalMin;

    [DataField]
    public TimeSpan EventIntervalMax;

    [DataField]
    public float ChaosScore;

    [DataField]
    public float MinStartingChaos;

    [DataField]
    public float MaxStartingChaos;

    [DataField]
    public float LivingChaosChange;

    [DataField]
    public float DeadChaosChange;

    [ViewVariables]
    public float ChaosChangeVariation = 1f;

    [DataField]
    public float ChaosChangeVariationMin = 1f;

    [DataField]
    public float ChaosChangeVariationMax = 1f;

    [DataField]
    public float ChaosChangeVariationExponent = 2f;

    [DataField]
    public float ChaosOffset = 50f;

    [DataField]
    public float ChaosExponent = 1.1f;

    [DataField]
    public float ChaosMatching = 1.8f;

    [DataField]
    public float ChaosThreshold = 20f;

    [DataField]
    public float MaximumAbsoluteChaos = 10000f;

    [DataField]
    public float ChaosDeadZone = 20f;

    [DataField]
    public float SpeedRamping;

    [DataField]
    public float MaximumRamping = 5f;

    [DataField]
    public TimeSpan MinimumEventInterval = TimeSpan.FromSeconds(30);

    [DataField]
    public int MinimumActivePlayers = 1;

    [DataField]
    public int MaximumGhostContribution = 20;

    [DataField]
    public bool NoRoundstartAntags;

    [DataField]
    public bool IgnoreTimings;

    [DataField]
    public bool IgnoreIncompatible;

    [DataField]
    public HashSet<ProtoId<EventTypePrototype>> DisallowedEvents = new();

    [ViewVariables]
    public List<SelectedEvent> SelectedEvents = new();

    [DataField]
    public ProtoId<WeightedRandomPrototype> PrimaryAntagsWeightTable = "SecretPlusPrimary";

    [DataField]
    public float PrimaryAntagChaosBias = 2f;

    [DataField]
    public int MaximumRoundstartRules = 10;

    [DataField]
    public ProtoId<WeightedRandomPrototype> RoundStartAntagsWeightTable = "SecretPlus";

    [DataField]
    public EntityTableSelector? ScheduledGameRules;
}
