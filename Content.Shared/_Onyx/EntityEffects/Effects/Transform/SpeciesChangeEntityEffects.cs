using Content.Shared.EntityEffects;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.EntityEffects.Effects.Transform;

public sealed partial class SpeciesChange : EntityEffectBase<SpeciesChange>
{
    [DataField(required: true)]
    public ProtoId<SpeciesPrototype> Species;
}

public sealed partial class RandomSpeciesChange : EntityEffectBase<RandomSpeciesChange>
{
    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>>? Whitelist;

    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>> Blacklist = [];
}
