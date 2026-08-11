using Content.Shared.Silicons.Laws.Components;

namespace Content.Shared.Silicons.Laws;

public abstract partial class SharedSiliconLawSystem
{
    public void SetProviderLawset(Entity<SiliconLawProviderComponent> provider, SiliconLawset lawset)
    {
        provider.Comp.Lawset = lawset;
        Dirty(provider);
    }
}
