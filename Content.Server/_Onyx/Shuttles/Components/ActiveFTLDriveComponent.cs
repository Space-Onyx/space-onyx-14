using Content.Shared._Onyx.Shuttles.Components;

namespace Content.Server._Onyx.Shuttles.Components;

[RegisterComponent]
public sealed partial class ActiveFTLDriveComponent : Component
{
    public FTLDriveData Data;
}
