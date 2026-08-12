namespace Content.Server._Onyx.SpaceWhale;

/// <summary>
/// Tracks mob-caller entity attached to a potential space whale target.
/// </summary>
[RegisterComponent]
public sealed partial class SpaceWhaleTargetComponent : Component
{
    [DataField]
    public EntityUid Entity;
}
