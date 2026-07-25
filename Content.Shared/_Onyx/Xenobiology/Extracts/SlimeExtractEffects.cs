using Content.Shared.EntityEffects;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Xenobiology.Extracts;

public sealed partial class UseSlimeExtract : EntityEffectBase<UseSlimeExtract>
{
    [DataField(required: true)]
    public EntityEffect[] Effects = [];
}

public sealed partial class ModifySlime : EntityEffectBase<ModifySlime>
{
    [DataField]
    public int ExtractBonus;

    [DataField]
    public int OffspringBonus;

    [DataField]
    public float ChanceModifier;
}

public sealed partial class AdjustExtractReagent : EntityEffectBase<AdjustExtractReagent>
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField(required: true)]
    public FixedPoint2 Amount;
}
