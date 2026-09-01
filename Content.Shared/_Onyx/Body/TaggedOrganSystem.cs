using Content.Shared.Body;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

public sealed partial class TaggedOrganSystem : EntitySystem
{
    [Dependency] private TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TaggedOrganComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<TaggedOrganComponent, OrganGotRemovedEvent>(OnRemoved);
    }

    private void OnInserted(Entity<TaggedOrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        var ownership = EnsureComp<OrganTagOwnershipComponent>(args.Target);
        foreach (var tag in ent.Comp.AddTags)
        {
            ownership.OriginalState.TryAdd(tag, _tags.HasTag(args.Target, tag));
            if (!ownership.AddedBy.TryGetValue(tag, out var sources))
                ownership.AddedBy[tag] = sources = new();
            sources.Add(ent.Owner);
            ReconcileTag(args.Target, tag, ownership);
        }

        foreach (var tag in ent.Comp.RemoveTags)
        {
            ownership.OriginalState.TryAdd(tag, _tags.HasTag(args.Target, tag));
            if (!ownership.RemovedBy.TryGetValue(tag, out var sources))
                ownership.RemovedBy[tag] = sources = new();
            sources.Add(ent.Owner);
            ReconcileTag(args.Target, tag, ownership);
        }
    }

    private void OnRemoved(Entity<TaggedOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (!TryComp(args.Target, out OrganTagOwnershipComponent? ownership))
            return;

        foreach (var tag in ent.Comp.AddTags)
        {
            if (!ownership.AddedBy.TryGetValue(tag, out var sources) || !sources.Remove(ent.Owner))
                continue;
            if (sources.Count == 0)
                ownership.AddedBy.Remove(tag);
            ReconcileTag(args.Target, tag, ownership);
        }

        foreach (var tag in ent.Comp.RemoveTags)
        {
            if (!ownership.RemovedBy.TryGetValue(tag, out var sources) || !sources.Remove(ent.Owner))
                continue;
            if (sources.Count == 0)
                ownership.RemovedBy.Remove(tag);
            ReconcileTag(args.Target, tag, ownership);
        }

        if (ownership.AddedBy.Count == 0 && ownership.RemovedBy.Count == 0)
            RemComp<OrganTagOwnershipComponent>(args.Target);
    }

    private void ReconcileTag(EntityUid body, ProtoId<TagPrototype> tag, OrganTagOwnershipComponent ownership)
    {
        if (ownership.RemovedBy.ContainsKey(tag))
        {
            _tags.RemoveTag(body, tag);
            return;
        }

        if (ownership.AddedBy.ContainsKey(tag))
        {
            _tags.AddTag(body, tag);
            return;
        }

        if (ownership.OriginalState.Remove(tag, out var originallyPresent) && originallyPresent)
            _tags.AddTag(body, tag);
        else
            _tags.RemoveTag(body, tag);
    }
}
