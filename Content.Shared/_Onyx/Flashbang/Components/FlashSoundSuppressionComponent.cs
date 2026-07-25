namespace Content.Shared._Onyx.Flashbang.Components;

[RegisterComponent]
public sealed partial class FlashSoundSuppressionComponent : Component
{
    [DataField]
    public float ProtectionRange = 2f;
}
