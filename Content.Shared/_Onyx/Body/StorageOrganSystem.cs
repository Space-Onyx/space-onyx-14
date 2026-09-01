using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;

namespace Content.Shared._Onyx.Body;

public sealed partial class StorageOrganSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StorageOrganComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<StorageOrganComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<StorageOrganComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StorageOrganComponent, OpenStorageOrganEvent>(OnOpen);
        SubscribeLocalEvent<BodyComponent, AccessibleOverrideEvent>(OnAccessible);
    }

    private void OnInserted(Entity<StorageOrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        RemoveAction(ent);
        _actions.AddAction(args.Target, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
        ent.Comp.ActionOwner = args.Target;
        Dirty(ent);
    }

    private void OnRemoved(Entity<StorageOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        _ui.CloseUi(ent.Owner, StorageComponent.StorageUiKey.Key);
        RemoveAction(ent);
    }

    private void OnShutdown(Entity<StorageOrganComponent> ent, ref ComponentShutdown args)
    {
        _ui.CloseUi(ent.Owner, StorageComponent.StorageUiKey.Key);
        RemoveAction(ent);
    }

    private void OnOpen(Entity<StorageOrganComponent> ent, ref OpenStorageOrganEvent args)
    {
        if (args.Handled || ent.Comp.ActionOwner != args.Performer || !TryComp(ent, out StorageComponent? storage))
            return;

        _storage.OpenStorageUI(ent.Owner, args.Performer, storage, false);
        args.Handled = true;
    }

    private void OnAccessible(Entity<BodyComponent> body, ref AccessibleOverrideEvent args)
    {
        if (args.Handled || args.User != body.Owner ||
            !TryComp(args.Target, out StorageOrganComponent? _) ||
            !TryComp(args.Target, out OrganComponent? organ) || organ.Body != body.Owner)
            return;

        args.Accessible = true;
        args.Handled = true;
    }

    private void RemoveAction(Entity<StorageOrganComponent> ent)
    {
        if (ent.Comp.ActionOwner is { } owner)
            _actions.RemoveAction(owner, ent.Comp.ActionEntity);
        ent.Comp.ActionEntity = null;
        ent.Comp.ActionOwner = null;
        Dirty(ent);
    }
}
