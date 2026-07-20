namespace Content.Server._Onyx.Gravity.Components;

[RegisterComponent]
public sealed partial class InheritedGridGravityComponent : Component
{
    public EntityUid SourceMap;
    public bool HadGravity;
    public bool PreviousEnabled;
    public bool PreviousInherent;
}
