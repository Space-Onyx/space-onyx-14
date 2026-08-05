using Content.Shared.Body;
using Content.Shared.Emp;
using Content.Shared.Interaction;

namespace Content.Shared._Onyx.Surgery.Augments;

public sealed partial class AugmentSystem
{
    private void InitializeLifecycle()
    {
        SubscribeLocalEvent<AugmentComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<AugmentComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<InstalledAugmentsComponent, AccessibleOverrideEvent>(OnAccessible);
        SubscribeLocalEvent<AugmentComponent, EmpPulseEvent>(OnEmp);
        SubscribeLocalEvent<AugmentComponent, EmpDisabledRemovedEvent>(OnEmpRemoved);
    }

    private void OnInserted(Entity<AugmentComponent> ent, ref OrganGotInsertedEvent args)
    {
        var installed = EnsureComp<InstalledAugmentsComponent>(args.Target);
        installed.Augments.Add(GetNetEntity(ent));
        Dirty(args.Target, installed);
        GrantAction(ent.Owner, args.Target);
        RefreshPower(args.Target);
    }

    private void OnRemoved(Entity<AugmentComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (TryComp(args.Target, out InstalledAugmentsComponent? installed))
        {
            installed.Augments.Remove(GetNetEntity(ent));
            if (installed.Augments.Count == 0)
                RemCompDeferred<InstalledAugmentsComponent>(args.Target);
            else
                Dirty(args.Target, installed);
        }
        RevokeAction(ent.Owner, args.Target);
        Disable(ent.Owner);
        RefreshPower(args.Target);
    }

    private void OnAccessible(Entity<InstalledAugmentsComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (GetBody(args.Target) != args.User)
            return;
        args.Handled = true;
        args.Accessible = true;
    }

    private void OnEmp(Entity<AugmentComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;
        Disable(ent.Owner);
        if (GetBody(ent.Owner) is { } body)
            RefreshPower(body);
    }

    private void OnEmpRemoved(Entity<AugmentComponent> ent, ref EmpDisabledRemovedEvent args)
    {
        if (GetBody(ent.Owner) is { } body)
            RefreshPower(body);
    }
}
