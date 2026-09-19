using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid.Markings;

namespace Content.Shared._Onyx.Body;

public sealed partial class OrganActionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBodySystem _body = default!;

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
        Refresh(ent, args.Target);
    }

    private void OnRemoved(Entity<OrganActionComponent> ent, ref OrganGotRemovedEvent args) => RemoveAction(ent);

    private void OnShutdown(Entity<OrganActionComponent> ent, ref ComponentShutdown args) => RemoveAction(ent);

    public void RefreshMarkingActions(EntityUid body)
    {
        foreach (var organ in _body.GetBodyOrgans(body))
            if (TryComp(organ.Id, out OrganActionComponent? action))
                Refresh((organ.Id, action), body);
    }

    private void Refresh(Entity<OrganActionComponent> ent, EntityUid body)
    {
        if (ent.Comp.MarkingLayers.Count > 0 &&
            (!TryComp(ent.Owner, out VisualOrganMarkingsComponent? visual) ||
             !ent.Comp.MarkingLayers.Any(layer => visual.Markings.TryGetValue(layer, out var markings) && markings.Count > 0)))
        {
            RemoveAction(ent);
            return;
        }

        if (ent.Comp.ActionEntity != null)
            return;

        _actions.AddAction(body, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
        ent.Comp.ActionOwner = body;
        Dirty(ent);
    }

    private void RemoveAction(Entity<OrganActionComponent> ent)
    {
        if (ent.Comp.ActionOwner is { } owner)
            _actions.RemoveAction(owner, ent.Comp.ActionEntity);

        ent.Comp.ActionEntity = null;
        ent.Comp.ActionOwner = null;
        Dirty(ent);
    }
}
