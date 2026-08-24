using Content.Shared._Onyx.Body.Prototypes;
using Content.Shared._Onyx.Body;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.EntitySystems;
using System.Linq;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body.Systems;

/// <summary>
/// Builds arbitrary body-part trees while leaving the existing surgery systems in control of runtime changes.
/// </summary>
public sealed partial class DeclarativeBodySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeclarativeBodyComponent, MapInitEvent>(
            OnMapInit,
            before: [typeof(InitialBodySystem)],
            after: [typeof(SharedHandsSystem)]);
    }

    private void OnMapInit(Entity<DeclarativeBodyComponent> entity, ref MapInitEvent args)
    {
        if (!TryComp(entity, out BodyComponent? bodyComponent)
            || bodyComponent.RootContainer is not { } rootContainer
            || rootContainer.ContainedEntity != null
            || !_prototypes.TryIndex(entity.Comp.Prototype, out var graph)
            || !Validate(graph, out var order))
            return;

        var spawned = new Dictionary<string, EntityUid>();
        foreach (var id in order)
        {
            var part = Spawn(graph.Slots[id].Part, Transform(entity).Coordinates);
            if (!TryComp<BodyPartComponent>(part, out _) || !HasComp<OrganComponent>(part))
            {
                QueueDel(part);
                Cleanup(spawned.Values);
                Log.Error($"Body graph {graph.ID} slot '{id}' spawned an invalid body part.");
                return;
            }

            spawned[id] = part;
        }

        foreach (var parentId in order)
        {
            var parent = spawned[parentId];
            foreach (var childId in graph.Slots[parentId].Connections)
            {
                var child = spawned[childId];
                var childPart = Comp<BodyPartComponent>(child);
                if (!_body.TryCreatePartSlot(parent, childId, childPart.PartType, childPart.Symmetry)
                    || !_body.TryAttachPart(parent, childId, child))
                {
                    Cleanup(spawned.Values);
                    Log.Error($"Body graph {graph.ID} failed to attach slot '{childId}' to '{parentId}'.");
                    return;
                }
            }
        }

        var root = spawned[graph.Root];
        foreach (var partId in order)
        {
            var part = spawned[partId];
            foreach (var (category, prototype) in graph.Slots[partId].Organs)
            {
                var organ = Spawn(prototype, Transform(entity).Coordinates);
                if (!_body.TryCreateOrganSlot(part, category.Id) || !_body.TryInsertOrgan(part, organ, category.Id))
                {
                    QueueDel(organ);
                    Cleanup(spawned.Values);
                    Log.Error($"Body graph {graph.ID} failed to insert organ '{category}' into '{partId}'.");
                    return;
                }
            }
        }

        if (!_containers.Insert(root, rootContainer, containerXform: Transform(entity)))
        {
            Cleanup(spawned.Values);
            Log.Error($"Body graph {graph.ID} failed to insert root '{graph.Root}'.");
            return;
        }

        _body.ActivateDeclarativeGraph(root, entity);
        _body.InitializeAnatomy(entity);

        RaiseLocalEvent(entity, new BodyGraphInitializedEvent());
    }

    private bool Validate(BodyGraphPrototype graph, out List<string> order)
    {
        var result = new List<string>();
        order = result;
        if (!graph.Slots.ContainsKey(graph.Root))
        {
            Log.Error($"Body graph {graph.ID} has unknown root '{graph.Root}'.");
            return false;
        }

        var parents = new HashSet<string>();
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        bool Visit(string id)
        {
            if (!visiting.Add(id))
                return false;
            if (!graph.Slots.TryGetValue(id, out var slot) || !_prototypes.HasIndex(slot.Part))
                return false;

            foreach (var organ in slot.Organs.Values)
                if (!_prototypes.HasIndex(organ))
                    return false;

            foreach (var child in slot.Connections)
            {
                if (!graph.Slots.ContainsKey(child) || !parents.Add(child) || !Visit(child))
                    return false;
            }

            visiting.Remove(id);
            visited.Add(id);
            result.Add(id);
            return true;
        }

        if (!Visit(graph.Root) || parents.Contains(graph.Root) || visited.Count != graph.Slots.Count)
        {
            Log.Error($"Body graph {graph.ID} must be a connected acyclic tree with one parent per slot.");
            order = result;
            return false;
        }

        result.Reverse();
        order = result;
        return true;
    }

    private void Cleanup(IEnumerable<EntityUid> entities)
    {
        foreach (var entity in entities)
            if (!TerminatingOrDeleted(entity))
                QueueDel(entity);
    }
}

public readonly record struct BodyGraphInitializedEvent;
