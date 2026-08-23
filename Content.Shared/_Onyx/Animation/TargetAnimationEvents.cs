namespace Content.Shared._Onyx.AnimationData;

[ImplicitDataDefinitionForInheritors, DataDefinition]
public abstract partial class BaseTargetEvent : EntityEventArgs
{
    public EntityUid Target;
}

[Serializable, DataDefinition]
public sealed partial class PlayAnimationTargetEvent : BaseTargetEvent
{
    [DataField, AlwaysPushInheritance] public string AnimationID = "";
}

[Serializable, DataDefinition]
public sealed partial class ApplyStatusEffectTargetEvent : BaseTargetEvent
{
    [DataField, AlwaysPushInheritance] public string Key = "";
    [DataField, AlwaysPushInheritance] public float Time;
    [DataField, AlwaysPushInheritance] public bool Refresh = true;
    [DataField, AlwaysPushInheritance] public string ComponentType = "";
}
