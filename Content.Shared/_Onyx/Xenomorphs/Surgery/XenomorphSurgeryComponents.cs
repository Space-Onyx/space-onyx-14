namespace Content.Shared._Onyx.Xenomorphs.Surgery;

public enum XenomorphSurgeryTarget
{
    Embryo,
    Larva,
}

[RegisterComponent]
public sealed partial class SurgeryXenomorphConditionComponent : Component
{
    [DataField(required: true)]
    public XenomorphSurgeryTarget Target;
}

[RegisterComponent]
public sealed partial class SurgeryRemoveXenomorphEffectComponent : Component
{
    [DataField(required: true)]
    public XenomorphSurgeryTarget Target;
}
