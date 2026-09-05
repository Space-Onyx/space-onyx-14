using Robust.Shared.Prototypes;
using Robust.Shared.Localization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Onyx.Weather;

[RegisterComponent]
[AutoGenerateComponentPause]
public sealed partial class WeatherSchedulerComponent : Component
{
    [DataField]
    public bool Random;

    [DataField(required: true)]
    public List<WeatherSchedulerStage> Stages = [];

    [DataField]
    public int CurrentStage;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTransition;

    public bool SchedulerActive;

    public string AnnouncementSender = string.Empty;
}

[DataDefinition]
public sealed partial class WeatherSchedulerStage
{
    [DataField]
    public EntProtoId? Weather;

    [DataField]
    public float Weight = 1f;

    [DataField(required: true)]
    public WeatherSchedulerDuration Duration = new();

    [DataField]
    public LocId? Announcement;
}

[DataDefinition]
public sealed partial class WeatherSchedulerDuration
{
    [DataField(required: true)]
    public float Min;

    [DataField(required: true)]
    public float Max;
}
