using Content.Shared.EntityEffects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Xenobiology.Extracts;

public sealed partial class DelayedExtractAreaReaction : EntityEffectBase<DelayedExtractAreaReaction>
{
    [DataField(required: true)]
    public EntProtoId PrototypeId;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField]
    public TimeSpan Delay;

    [DataField]
    public float Duration = 10f;

    [DataField]
    public int SpreadAmount = 1;
}
