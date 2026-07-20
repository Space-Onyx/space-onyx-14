using Content.Server.Radio.EntitySystems;

namespace Content.Server._Onyx.Radio.Components;

[RegisterComponent]
[Access(typeof(RadioDeviceSystem))]
public sealed partial class HandheldRadioPresetComponent : Component
{
    [DataField]
    public List<string> Channels = new()
    {
        "Handheld",
        "HandheldPreset2",
        "HandheldPreset3",
        "HandheldPreset4",
        "HandheldPreset5",
    };

    [DataField]
    public int CurrentIndex;
}
