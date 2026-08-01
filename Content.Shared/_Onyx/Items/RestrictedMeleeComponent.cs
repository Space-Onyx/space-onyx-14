using Content.Shared.Whitelist;
using Robust.Shared.Audio;

namespace Content.Shared._Onyx.Items;

[RegisterComponent]
public sealed partial class RestrictedMeleeComponent : Component
{
    [DataField] public EntityWhitelist? Whitelist;
    [DataField] public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(2);
    [DataField] public string FailText = "restricted-melee-component-attack-fail-too-large";
    [DataField] public bool DoKnockdown = true;
    [DataField] public bool ForceDrop = true;
    [DataField] public SoundSpecifier FallSound = new SoundPathSpecifier("/Audio/Effects/slip.ogg");
}
