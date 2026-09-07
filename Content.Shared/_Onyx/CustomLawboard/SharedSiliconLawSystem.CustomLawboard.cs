using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.Laws;

public abstract partial class SharedSiliconLawSystem
{
    public void SetProviderLawset(Entity<SiliconLawProviderComponent> provider, SiliconLawset lawset)
    {
        provider.Comp.Lawset = lawset;
        Dirty(provider);
    }

    public void SetProviderLawset(Entity<SiliconLawProviderComponent> provider, ProtoId<SiliconLawsetPrototype> lawset)
    {
        provider.Comp.Laws = lawset;
        provider.Comp.Lawset = null;
        Dirty(provider);
    }
}
