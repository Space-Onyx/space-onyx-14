namespace Content.Shared._Onyx.Construction;

[RegisterComponent]
public sealed partial class ChangeConstructionNodeOnIgniteComponent : Component
{
    [DataField(required: true)]
    public string TargetNode = string.Empty;
}
