using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Chat;

[Prototype]
public sealed partial class CustomEmoteSoundPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name = default!;

    [DataField(required: true)]
    public SoundSpecifier Sound { get; private set; } = default!;
}
