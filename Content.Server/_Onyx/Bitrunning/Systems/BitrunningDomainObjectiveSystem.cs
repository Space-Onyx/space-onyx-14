using Content.Shared._Onyx.Bitrunning;
using Content.Shared._Onyx.Bitrunning.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server._Onyx.Bitrunning.Systems;

public sealed partial class BitrunningDomainObjectiveSystem : EntitySystem
{
    [Dependency] private QuantumServerSystem _server = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BitrunningDomainObjectiveComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<BitrunningDomainObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<BitrunningDomainObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAssigned(Entity<BitrunningDomainObjectiveComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.Server = EntityUid.Invalid;
    }

    private void OnAfterAssign(Entity<BitrunningDomainObjectiveComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (ent.Comp.Server == EntityUid.Invalid
            || !TryComp<QuantumServerComponent>(ent.Comp.Server, out var server)
            || server.CurrentDomain is not { } domainId)
            return;

        _server.SetDomainObjectiveMetadata(ent.Owner, server, domainId, args.Meta);
    }

    private void OnGetProgress(Entity<BitrunningDomainObjectiveComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<QuantumServerComponent>(ent.Comp.Server, out var server) || server.ObjectiveGoal <= 0)
            return;

        args.Progress = Math.Clamp((float) server.ObjectivePoints / server.ObjectiveGoal, 0f, 1f);
    }
}
