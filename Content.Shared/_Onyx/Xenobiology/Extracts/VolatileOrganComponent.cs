namespace Content.Shared._Onyx.Xenobiology.Extracts;

[RegisterComponent]
public sealed partial class VolatileOrganComponent : Component
{
    [DataField]
    public int ArcDepth = 1;

    [DataField]
    public int MaxLightningArcs = 3;

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(300);

    [DataField]
    public float Range = 5f;

    [ViewVariables]
    public TimeSpan NextArc;
}

[RegisterComponent]
public sealed partial class VolatileOrganUserComponent : Component
{
    [ViewVariables]
    public EntityUid Organ;
}
