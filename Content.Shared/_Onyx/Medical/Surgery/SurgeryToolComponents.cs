using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Medical.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSurgerySystem), typeof(SurgeryToolExamineSystem))]
public sealed partial class SurgeryToolComponent : Component
{
    [DataField, AutoNetworkedField] public Dictionary<string, float> SpeedModifiers = new();
    [DataField, AutoNetworkedField] public List<LocId> CustomUses = new();
    [DataField, AutoNetworkedField] public SoundSpecifier? StartSound;
    [DataField, AutoNetworkedField] public SoundSpecifier? EndSound;
}

[RegisterComponent] public sealed partial class ScalpelComponent : Component;
[RegisterComponent] public sealed partial class HemostatComponent : Component;
[RegisterComponent] public sealed partial class RetractorComponent : Component;
[RegisterComponent] public sealed partial class BoneSawComponent : Component;
[RegisterComponent] public sealed partial class CauteryComponent : Component;
[RegisterComponent] public sealed partial class BoneGelComponent : Component;
[RegisterComponent] public sealed partial class TweezersComponent : Component;
[RegisterComponent] public sealed partial class DrillComponent : Component;
[RegisterComponent] public sealed partial class StitchesComponent : Component;
[RegisterComponent] public sealed partial class BoneSetterComponent : Component;
[RegisterComponent] public sealed partial class TendingComponent : Component;
