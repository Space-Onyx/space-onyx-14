using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.CollectiveMind;

public sealed class CollectiveMindSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CollectiveMindComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<CollectiveMindComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.DefaultChannel is not { } channel || ent.Comp.Channels.Contains(channel))
            return;

        ent.Comp.Channels.Add(channel);
        Dirty(ent);
    }
}
