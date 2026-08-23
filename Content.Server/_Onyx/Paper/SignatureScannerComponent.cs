using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Paper;

[RegisterComponent]
public sealed partial class SignatureScannerComponent : Component
{
    public TimeSpan PrintReadyAt = TimeSpan.Zero;

    [DataField("printCooldown")]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(5);

    [DataField("soundPrint")]
    public SoundSpecifier SoundPrint = new SoundPathSpecifier("/Audio/Machines/short_print_and_rip.ogg");

    [DataField("machineOutput")]
    public EntProtoId MachineOutput = "SignatureScannerReportPaper";
}
