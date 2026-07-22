namespace Content.Server._Onyx.Weapons;

[RegisterComponent]
public sealed partial class TriggerOnVoiceIdLockedComponent : Component
{
    [DataField]
    public LocId KeyPhrase;

    [DataField]
    public TimeSpan ActivationCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public bool HolderOnly;

    public TimeSpan NextActivationTime;
    public EntityUid? User;
}
