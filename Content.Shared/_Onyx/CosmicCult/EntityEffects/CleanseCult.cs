using Content.Shared._Onyx.CosmicCult.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.CosmicCult.EntityEffects;

public sealed partial class CleanseCultEntityEffectSystem : EntityEffectSystem<MetaDataComponent, CleanseCult>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<CleanseCult> args)
    {
        if (HasComp<CosmicCultComponent>(entity) || HasComp<RogueAscendedInfectionComponent>(entity))
            EnsureComp<CleanseCultComponent>(entity);
    }
}

public sealed partial class CleanseCult : EntityEffectBase<CleanseCult>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-cleanse-cultist", ("chance", Probability));
}
