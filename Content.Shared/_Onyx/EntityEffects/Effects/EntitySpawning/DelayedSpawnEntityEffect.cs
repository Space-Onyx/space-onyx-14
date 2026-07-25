using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.EntityEffects.Effects.EntitySpawning;

public sealed partial class DelayedSpawnEntity : EntityEffectBase<DelayedSpawnEntity>
{
    [DataField(required: true)]
    public EntProtoId Entity;

    [DataField]
    public int Number = 1;

    [DataField]
    public TimeSpan Delay;
}
