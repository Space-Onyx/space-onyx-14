using Content.Shared.Body;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Body;

/// <summary>
/// Applies <see cref="OrganComponent.OnAdd"/> components to the body when the organ is inserted,
/// and removes them when the organ is taken out. Mirrors the Goob-Station OrganEffectSystem behavior.
/// </summary>
public sealed partial class OrganEffectSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, OrganGotInsertedEvent>(OnGotInserted);
        SubscribeLocalEvent<OrganComponent, OrganGotRemovedEvent>(OnGotRemoved);
    }

    private void OnGotInserted(Entity<OrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.OnAdd is not { } onAdd)
            return;

        EntityManager.AddComponents(args.Target, onAdd);
    }

    private void OnGotRemoved(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.OnAdd is not { } onAdd)
            return;

        EntityManager.RemoveComponents(args.Target, onAdd);
    }
}