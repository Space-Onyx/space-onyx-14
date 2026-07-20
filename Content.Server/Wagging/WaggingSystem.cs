using Content.Server.Actions;
using Content.Shared.Body;
using Content.Shared.Cloning.Events;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Toggleable;
using Content.Shared.Wagging;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Wagging;

/// <summary>
/// Adds an action to toggle wagging animation for tails markings that supporting this
/// </summary>
public sealed partial class WaggingSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WaggingComponent, MapInitEvent>(OnWaggingMapInit);
        // <Onyx-DynamicWagging>
        SubscribeLocalEvent<WaggingComponent, ComponentStartup>(OnWaggingStartup);
        SubscribeLocalEvent<VisualBodyComponent, VisualBodyMarkingsChangedEvent>(OnMarkingsChanged);
        // </Onyx-DynamicWagging>
        SubscribeLocalEvent<WaggingComponent, ComponentShutdown>(OnWaggingShutdown);
        SubscribeLocalEvent<WaggingComponent, ToggleActionEvent>(OnWaggingToggle);
        SubscribeLocalEvent<WaggingComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<WaggingComponent, CloningEvent>(OnCloning);
    }

    private void OnCloning(Entity<WaggingComponent> ent, ref CloningEvent args)
    {
        if (!args.Settings.EventComponents.Contains(Factory.GetRegistration(ent.Comp.GetType()).Name))
            return;

        // Make sure to set the datafields before adding the component so that the correct action gets spawned on map init.
        var cloneComp = Factory.GetComponent<WaggingComponent>();
        cloneComp.Action = ent.Comp.Action;
        cloneComp.Layer = ent.Comp.Layer;
        cloneComp.Organ = ent.Comp.Organ;
        cloneComp.Suffix = ent.Comp.Suffix;
        AddComp(args.CloneUid, cloneComp, true);
    }

    private void OnWaggingMapInit(Entity<WaggingComponent> ent, ref MapInitEvent args)
    {
        EnsureWaggingAction(ent);
    }

    // <Onyx-DynamicWagging>
    private void OnWaggingStartup(Entity<WaggingComponent> ent, ref ComponentStartup args)
    {
        EnsureWaggingAction(ent);
    }

    private void EnsureWaggingAction(Entity<WaggingComponent> ent)
    {
        if (ent.Comp.ActionEntity != null)
            return;

        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }

    private void OnMarkingsChanged(Entity<VisualBodyComponent> ent, ref VisualBodyMarkingsChangedEvent args)
    {
        if (TryGetWaggableTail(ent, out var organ, out var layer))
        {
            var wagging = EnsureComp<WaggingComponent>(ent);
            wagging.Organ = organ;
            wagging.Layer = layer;
            return;
        }

        RemComp<WaggingComponent>(ent);
    }

    private bool TryGetWaggableTail(Entity<VisualBodyComponent> ent,
        out ProtoId<OrganCategoryPrototype> organ,
        out HumanoidVisualLayers layer)
    {
        organ = default;
        layer = HumanoidVisualLayers.Tail;
        if (!_visualBody.TryGatherMarkingsData(ent.AsNullable(), [layer], out _, out _, out var applied))
            return false;

        foreach (var (category, markingSet) in applied)
        {
            if (!markingSet.TryGetValue(layer, out var tails))
                continue;

            foreach (var tail in tails)
            {
                var id = tail.MarkingId.Id;
                if (id.EndsWith(WaggingComponent.DefaultSuffix)
                    ? _prototype.HasIndex<MarkingPrototype>(id[..^WaggingComponent.DefaultSuffix.Length])
                    : _prototype.HasIndex<MarkingPrototype>($"{id}{WaggingComponent.DefaultSuffix}"))
                {
                    organ = category;
                    return true;
                }
            }
        }

        return false;
    }
    // </Onyx-DynamicWagging>

    private void OnWaggingShutdown(Entity<WaggingComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnWaggingToggle(Entity<WaggingComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        TryToggleWagging(ent.AsNullable());
    }

    private void OnMobStateChanged(Entity<WaggingComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.Wagging)
            TryToggleWagging(ent.AsNullable());
    }

    private bool TryToggleWagging(Entity<WaggingComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!_visualBody.TryGatherMarkingsData(ent.Owner,
                [ent.Comp.Layer],
                out _,
                out _,
                out var applied))
        {
            return false;
        }

        if (!applied.TryGetValue(ent.Comp.Organ, out var markingsSet))
            return false;

        ent.Comp.Wagging = !ent.Comp.Wagging;

        markingsSet = markingsSet.ShallowClone();
        foreach (var (layers, markings) in markingsSet)
        {
            markingsSet[layers] = markingsSet[layers].ShallowClone();
            var layerMarkings = markingsSet[layers];

            for (int i = 0; i < layerMarkings.Count; i++)
            {
                var currentMarkingId = layerMarkings[i].MarkingId;
                string newMarkingId;

                if (ent.Comp.Wagging)
                {
                    newMarkingId = $"{currentMarkingId}{ent.Comp.Suffix}";
                }
                else
                {
                    if (currentMarkingId.Id.EndsWith(ent.Comp.Suffix))
                    {
                        newMarkingId = currentMarkingId.Id[..^ent.Comp.Suffix.Length];
                    }
                    else
                    {
                        newMarkingId = currentMarkingId;
                        Log.Warning($"Unable to revert wagging for {currentMarkingId}");
                    }
                }

                if (!ProtoMan.HasIndex<MarkingPrototype>(newMarkingId))
                {
                    Log.Warning($"{ToPrettyString(ent):ent} tried toggling wagging but {newMarkingId} marking doesn't exist");
                    continue;
                }

                layerMarkings[i] = new Marking(newMarkingId, layerMarkings[i].MarkingColors);
            }
        }

        _visualBody.ApplyMarkings(ent, new()
        {
            [ent.Comp.Organ] = markingsSet
        });
        return true;
    }
}
