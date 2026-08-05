using Content.Shared.Actions;
using Content.Shared.Body;

namespace Content.Shared._Onyx.Body;

public sealed partial class OrganActionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganActionComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<OrganActionComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<OrganActionComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInserted(Entity<OrganActionComponent> ent, ref OrganGotInsertedEvent args)
    {
        RemoveAction(ent);
        _actions.AddAction(args.Target, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
        ent.Comp.ActionOwner = args.Target;
        Dirty(ent);
    }

    private void OnRemoved(Entity<OrganActionComponent> ent, ref OrganGotRemovedEvent args) => RemoveAction(ent);

    private void OnShutdown(Entity<OrganActionComponent> ent, ref ComponentShutdown args) => RemoveAction(ent);

    private void RemoveAction(Entity<OrganActionComponent> ent)
    {
        if (ent.Comp.ActionOwner is { } owner)
            _actions.RemoveAction(owner, ent.Comp.ActionEntity);

        ent.Comp.ActionEntity = null;
        ent.Comp.ActionOwner = null;
        Dirty(ent);
    }
}
