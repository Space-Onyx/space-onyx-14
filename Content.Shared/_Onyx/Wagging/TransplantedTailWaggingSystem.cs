using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Wagging;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wagging;

public sealed partial class TransplantedTailWaggingSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, BodyOrganSlotChangedEvent>(OnOrganSlotChanged);
        SubscribeLocalEvent<VisualBodyComponent, VisualBodyMarkingsChangedEvent>(OnMarkingsChanged);
    }

    private void OnMarkingsChanged(Entity<VisualBodyComponent> ent, ref VisualBodyMarkingsChangedEvent args)
    {
        if (!TryGetWaggableTail(ent, out var category))
        {
            RemComp<WaggingComponent>(ent.Owner);
            return;
        }

        var wagging = EnsureComp<WaggingComponent>(ent.Owner);
        wagging.Organ = category;
        wagging.Layer = HumanoidVisualLayers.Tail;
        if (wagging.ActionEntity == null)
            _actions.AddAction(ent.Owner, ref wagging.ActionEntity, wagging.Action, ent.Owner);
    }

    private void OnOrganSlotChanged(Entity<BodyComponent> ent, ref BodyOrganSlotChangedEvent args)
    {
        if (args.Slot != "Tail")
            return;

        if (!args.Inserted)
        {
            RemComp<WaggingComponent>(ent.Owner);
            return;
        }

        if (!TryComp<OrganComponent>(args.Organ, out var organ) ||
            organ.Category is not { } category ||
            !TryComp<VisualOrganMarkingsComponent>(args.Organ, out var visualMarkings) ||
            !HasWaggableTail(visualMarkings.Markings))
            return;

        var wagging = EnsureComp<WaggingComponent>(ent.Owner);
        wagging.Organ = category;
        wagging.Layer = HumanoidVisualLayers.Tail;
        if (wagging.ActionEntity == null)
            _actions.AddAction(ent.Owner, ref wagging.ActionEntity, wagging.Action, ent.Owner);
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
                return true;
        }

        return false;
    }

    private bool TryGetWaggableTail(Entity<VisualBodyComponent> body, out ProtoId<OrganCategoryPrototype> category)
    {
        category = default;
        if (!_visualBody.TryGatherMarkingsData(body.AsNullable(), [HumanoidVisualLayers.Tail], out _, out _, out var applied))
            return false;

        foreach (var (organ, markings) in applied)
        {
            if (markings.TryGetValue(HumanoidVisualLayers.Tail, out var tails) &&
                tails.Any(tail => HasAnimatedMarking(tail.MarkingId.Id)))
            {
                category = organ;
                return true;
            }
        }

        return false;
    }

    private bool HasAnimatedMarking(string id)
    {
        return id.EndsWith(WaggingComponent.DefaultSuffix)
            ? ProtoMan.HasIndex<MarkingPrototype>(id[..^WaggingComponent.DefaultSuffix.Length])
            : ProtoMan.HasIndex<MarkingPrototype>($"{id}{WaggingComponent.DefaultSuffix}");
    }
}
