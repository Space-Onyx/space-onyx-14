using Content.Server.Actions;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Wagging;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Wagging;

public sealed partial class TransplantedTailWaggingSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BodyComponent, BodyOrganSlotChangedEvent>(OnOrganSlotChanged);
    }

    private void OnOrganSlotChanged(Entity<BodyComponent> ent, ref BodyOrganSlotChangedEvent args)
    {
        if (args.Slot != "Tail")
            return;

        if (!args.Inserted)
        {
            RemComp<WaggingComponent>(ent);
            return;
        }

        if (!TryComp<OrganComponent>(args.Organ, out var organ)
            || organ.Category is not { } category
            || !TryComp<VisualOrganMarkingsComponent>(args.Organ, out var visualMarkings)
            || !HasWaggableTail(visualMarkings.Markings))
        {
            return;
        }

        var wagging = EnsureComp<WaggingComponent>(ent);
        wagging.Organ = category;
        wagging.Layer = HumanoidVisualLayers.Tail;
        if (wagging.ActionEntity == null)
            _actions.AddAction(ent, ref wagging.ActionEntity, wagging.Action, ent);
    }

    private bool HasWaggableTail(Dictionary<HumanoidVisualLayers, List<Marking>> markings)
    {
        if (!markings.TryGetValue(HumanoidVisualLayers.Tail, out var tails))
            return false;

        foreach (var tail in tails)
        {
            var id = tail.MarkingId.Id;
            if (id.EndsWith(WaggingComponent.DefaultSuffix)
                ? ProtoMan.HasIndex<MarkingPrototype>(id[..^WaggingComponent.DefaultSuffix.Length])
                : ProtoMan.HasIndex<MarkingPrototype>($"{id}{WaggingComponent.DefaultSuffix}"))
            {
                return true;
            }
        }

        return false;
    }
}
