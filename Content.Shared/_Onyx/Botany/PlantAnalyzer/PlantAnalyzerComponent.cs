using Content.Shared._Onyx.AbstractAnalyzer;
using Content.Shared.Paper;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Onyx.Botany.PlantAnalyzer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class PlantAnalyzerComponent : AbstractAnalyzerComponent
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public override TimeSpan NextUpdate { get; set; } = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan PrintReadyAt = TimeSpan.Zero;

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier SoundPrint = new SoundPathSpecifier("/Audio/Machines/short_print_and_rip.ogg");

    [DataField]
    public EntProtoId<PaperComponent> MachineOutput = "PlantAnalyzerReportPaper";
}
