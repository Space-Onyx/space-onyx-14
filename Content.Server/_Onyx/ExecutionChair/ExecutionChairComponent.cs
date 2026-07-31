using Robust.Shared.Audio;

namespace Content.Server._Onyx.ExecutionChair;

[RegisterComponent, Access(typeof(ExecutionChairSystem))]
public sealed partial class ExecutionChairComponent : Component
{
    [ViewVariables] public TimeSpan NextDamageTick;
    [DataField, AutoNetworkedField] public bool Enabled;
    [DataField] public bool PlaySoundOnShock = true;
    [DataField] public SoundSpecifier ShockNoises = new SoundCollectionSpecifier("sparks");
    [DataField] public float ShockVolume = 20;
    [DataField] public int DamagePerTick = 25;
    [DataField] public int DamageTime = 4;
    [DataField] public string TogglePort = "Toggle";
    [DataField] public string OnPort = "On";
    [DataField] public string OffPort = "Off";
}
