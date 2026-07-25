using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Mobs.Growth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class MobGrowthComponent : Component
{
    [DataField(required: true)]
    public string InitialStage = string.Empty;

    [DataField]
    [AutoNetworkedField]
    public string CurrentStage = string.Empty;

    [DataField(required: true, readOnly: true)]
    public Dictionary<string, MobGrowthStage> Stages = new();

    [DataField]
    public float HungerRequired = 100f;

    [DataField]
    public float HungerCost = 75f;

    [DataField]
    public TimeSpan GrowthInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextGrowth;
}

[DataDefinition]
public sealed partial class MobGrowthStage
{
    [DataField]
    public string? NextStage;

    [DataField]
    public ResPath? Sprite;

    [DataField]
    public string? State;

    [DataField]
    public int Layer;

    [DataField]
    public LocId? NamePrefix;

    [DataField]
    public LocId? Description;
}

[Serializable, NetSerializable]
public enum MobGrowthVisuals : byte
{
    Stage,
}

[ByRefEvent]
public readonly record struct MobGrowthStageChangedEvent(string OldStage, string NewStage);
