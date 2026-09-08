using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.DeviceLinking;

[RegisterComponent]
public sealed partial class MiscSignallerComponent : Component
{
    [DataField]
    public ProtoId<SourcePortPrototype> Port = "Triggered";

    [DataField]
    public TimeSpan ActivationInterval = TimeSpan.FromSeconds(3);

    public TimeSpan NextActivation;
}
