using System.Linq;
using Content.Shared._Onyx.Clothing; // <Onyx-ClothingDirt>
using Content.Shared._Onyx.Humanoid; // <Onyx-MarkingBounds>
using Content.Client.DisplacementMap;
using Content.Shared.Body;
using Content.Shared.Body.Part; // <Onyx-OrganVisualLifecycle>
using Content.Shared.CCVar;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Body;

public sealed partial class VisualBodySystem : SharedVisualBodySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DisplacementMapSystem _displacement = default!;
    [Dependency] private MarkingManager _marking = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    // <Onyx-OrganVisualLifecycle>
    private readonly Dictionary<EntityUid, EntityUid> _visualTargets = new();
    private readonly Dictionary<EntityUid, EntityUid> _markingTargets = new();
    // </Onyx-OrganVisualLifecycle>

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisualOrganComponent, OrganGotInsertedEvent>(OnOrganGotInserted);
        SubscribeLocalEvent<VisualOrganComponent, OrganGotRemovedEvent>(OnOrganGotRemoved);
        SubscribeLocalEvent<VisualOrganComponent, AfterAutoHandleStateEvent>(OnOrganState);
        SubscribeLocalEvent<VisualOrganComponent, ComponentShutdown>(OnVisualShutdown);

        SubscribeLocalEvent<VisualOrganMarkingsComponent, OrganGotInsertedEvent>(OnMarkingsGotInserted);
        SubscribeLocalEvent<VisualOrganMarkingsComponent, OrganGotRemovedEvent>(OnMarkingsGotRemoved);
        SubscribeLocalEvent<VisualOrganMarkingsComponent, AfterAutoHandleStateEvent>(OnMarkingsState);
        SubscribeLocalEvent<VisualOrganMarkingsComponent, ComponentShutdown>(OnMarkingsShutdown);

        // <Onyx-OrganVisualLifecycle>
        SubscribeLocalEvent<OrganComponent, AfterAutoHandleStateEvent>(OnOrganRelationshipState);
        // </Onyx-OrganVisualLifecycle>

        SubscribeLocalEvent<VisualOrganMarkingsComponent, BodyRelayedEvent<HumanoidLayerVisibilityChangedEvent>>(OnMarkingsChangedVisibility);

        Subs.CVar(_cfg, CCVars.AccessibilityClientCensorNudity, OnCensorshipChanged, true);
        Subs.CVar(_cfg, CCVars.AccessibilityServerCensorNudity, OnCensorshipChanged, true);
    }

    private void OnCensorshipChanged(bool value)
    {
        var query = AllEntityQuery<OrganComponent, VisualOrganMarkingsComponent>();
        while (query.MoveNext(out var ent, out var organComp, out var markingsComp))
        {
            if (organComp.Body is not { } body)
                continue;

            RemoveMarkings((ent, markingsComp), body);
            ApplyMarkings((ent, markingsComp), body);
        }

        var partQuery = AllEntityQuery<Content.Shared.Body.Part.BodyPartComponent, VisualOrganMarkingsComponent>();
        while (partQuery.MoveNext(out var ent, out var part, out var markingsComp))
        {
            // <Onyx-DetachedPartVisuals-edited>
            var target = GetVisualTarget(ent);
            // </Onyx-DetachedPartVisuals-edited>
            RemoveMarkings((ent, markingsComp), target);
            ApplyMarkings((ent, markingsComp), target);
        }
    }

    private void OnOrganGotInserted(Entity<VisualOrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        // <Onyx-DetachedPartVisuals-edited>
        RemoveVisual(ent, GetDetachedPartRoot(ent.Owner));
        // </Onyx-DetachedPartVisuals-edited>
        ApplyVisual(ent, args.Target);
        _visualTargets[ent.Owner] = args.Target;
    }

    private void OnOrganGotRemoved(Entity<VisualOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        RemoveVisual(ent, args.Target);
        // <Onyx-DetachedPartVisuals-edited>
        var detachedRoot = GetDetachedPartRoot(ent.Owner);
        if (detachedRoot == ent.Owner && HasComp<Content.Shared.Body.Part.BodyPartComponent>(ent.Owner))
            ClearDetachedBodyPartVisuals(detachedRoot);

        ApplyVisual(ent, detachedRoot);
        _visualTargets[ent.Owner] = detachedRoot;
        // </Onyx-DetachedPartVisuals-edited>
    }

    private void OnOrganState(Entity<VisualOrganComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ReconcileVisual(ent, true);
        RaiseLocalEvent(ent, new BodyPartVisualChangedEvent()); // <Onyx-ClothingDirt>
    }

    private void ApplyVisual(Entity<VisualOrganComponent> ent, EntityUid target)
    {
        // <Onyx-DetachedOrganVisuals-edited>
        if (!ent.Comp.ShowOnDetached &&
            TryComp<OrganComponent>(ent.Owner, out var organ) &&
            organ.Body == null)
            return;
        // </Onyx-DetachedOrganVisuals-edited>

        var index = _sprite.LayerMapTryGet(target, ent.Comp.Layer, out var existing, false)
            ? existing
            : _sprite.LayerMapReserve(target, ent.Comp.Layer);

        _sprite.LayerSetData(target, index, ent.Comp.Data);
        // <Onyx-CyberneticVisuals-edited>
        // LayerSetData preserves the old layer tint when Data.Color is null.
        if (!ent.Comp.ColorFromProfile)
            _sprite.LayerSetColor(target, index, ent.Comp.Data.Color ?? Color.White);
        // </Onyx-CyberneticVisuals-edited>

        var displacement = ent.Comp.Displacement;
        if (displacement != null && ProtoMan.Resolve(displacement, out var displacementProto))
        {
            _displacement.TryAddDisplacement(displacementProto.Displacement,
                (target, Comp<SpriteComponent>(target)),
                index,
                ent.Comp.Layer,
                out _);
        }

        if (displacement == null)
        {
            _displacement.EnsureDisplacementIsNotOnSprite((target, Comp<SpriteComponent>(target)), ent.Comp.Layer);
        }
    }

    private void RemoveVisual(Entity<VisualOrganComponent> ent, EntityUid target)
    {
        if (!_sprite.LayerMapTryGet(target, ent.Comp.Layer, out var index, false))
            return;

        _sprite.LayerSetRsiState(target, index, RSI.StateId.Invalid);

        _displacement.EnsureDisplacementIsNotOnSprite((target, Comp<SpriteComponent>(target)), ent.Comp.Layer);
    }

    private void OnMarkingsGotInserted(Entity<VisualOrganMarkingsComponent> ent, ref OrganGotInsertedEvent args)
    {
        // <Onyx-DetachedPartVisuals-edited>
        RemoveMarkings(ent, GetDetachedPartRoot(ent.Owner));
        // </Onyx-DetachedPartVisuals-edited>
        ApplyMarkings(ent, args.Target);
        _markingTargets[ent.Owner] = args.Target;
    }

    private void OnMarkingsGotRemoved(Entity<VisualOrganMarkingsComponent> ent, ref OrganGotRemovedEvent args)
    {
        RemoveMarkings(ent, args.Target);
        // <Onyx-DetachedPartVisuals-edited>
        ApplyMarkings(ent, GetDetachedPartRoot(ent.Owner));
        _markingTargets[ent.Owner] = GetDetachedPartRoot(ent.Owner);
        // </Onyx-DetachedPartVisuals-edited>
    }

    private void OnMarkingsState(Entity<VisualOrganMarkingsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ReconcileMarkings(ent, true);
    }

    // <Onyx-OrganVisualLifecycle>
    private void OnOrganRelationshipState(Entity<OrganComponent> ent, ref AfterAutoHandleStateEvent args) =>
        ReconcileRelationshipVisuals(ent.Owner);

    private void ReconcileRelationshipVisuals(EntityUid entity)
    {
        if (TryComp(entity, out VisualOrganComponent? visual))
            ReconcileVisual((entity, visual), false);
        if (TryComp(entity, out VisualOrganMarkingsComponent? markings))
            ReconcileMarkings((entity, markings), false);
    }

    private void ReconcileVisual(Entity<VisualOrganComponent> ent, bool force)
    {
        var target = GetVisualTarget(ent.Owner);
        if (_visualTargets.TryGetValue(ent.Owner, out var previous))
        {
            if (previous == target && !force)
                return;
            if (Exists(previous))
                RemoveVisual(ent, previous);
        }

        ApplyVisual(ent, target);
        _visualTargets[ent.Owner] = target;
    }

    private void ReconcileMarkings(Entity<VisualOrganMarkingsComponent> ent, bool force)
    {
        var target = GetVisualTarget(ent.Owner);
        if (_markingTargets.TryGetValue(ent.Owner, out var previous))
        {
            if (previous == target && !force)
                return;
            if (Exists(previous))
                RemoveMarkings(ent, previous);
        }

        ApplyMarkings(ent, target);
        _markingTargets[ent.Owner] = target;
    }

    private void OnVisualShutdown(Entity<VisualOrganComponent> ent, ref ComponentShutdown args) =>
        _visualTargets.Remove(ent.Owner);

    private void OnMarkingsShutdown(Entity<VisualOrganMarkingsComponent> ent, ref ComponentShutdown args) =>
        _markingTargets.Remove(ent.Owner);
    // </Onyx-OrganVisualLifecycle>

    protected override void SetOrganColor(Entity<VisualOrganComponent> ent, Color color)
    {
        base.SetOrganColor(ent, color);

        ApplyVisual(ent, GetVisualTarget(ent));
    }

    protected override void SetOrganMarkings(Entity<VisualOrganMarkingsComponent> ent, Dictionary<HumanoidVisualLayers, List<Marking>> markings, Dictionary<HumanoidVisualLayers, DisplacementData> displacement, HashSet<Enum> hideability)
    {
        base.SetOrganMarkings(ent, markings, displacement, hideability);

        var body = GetVisualTarget(ent);
        RemoveMarkings(ent, body);
        ApplyMarkings(ent, body);
    }

    // <Onyx-DetachedPartVisuals-edited>
    private EntityUid GetVisualTarget(EntityUid entity)
    {
        if (CompOrNull<OrganComponent>(entity)?.Body is { } organBody)
            return organBody;

        if (CompOrNull<Content.Shared.Body.Part.BodyPartComponent>(entity)?.Body is { } partBody)
            return partBody;

        return GetDetachedPartRoot(entity);
    }

    private EntityUid GetDetachedPartRoot(EntityUid entity)
    {
        var root = entity;
        while (CompOrNull<Content.Shared.Body.Part.BodyPartComponent>(root)?.Parent is { } parent)
            root = parent;

        return root;
    }
    // </Onyx-DetachedPartVisuals-edited>

    protected override void SetOrganAppearance(Entity<VisualOrganComponent> ent, PrototypeLayerData data, ProtoId<DisplacementDataPrototype>? displacement)
    {
        base.SetOrganAppearance(ent, data, displacement);

        ApplyVisual(ent, GetVisualTarget(ent));
    }

    private IEnumerable<Marking> AllMarkings(Entity<VisualOrganMarkingsComponent> ent)
    {
        foreach (var markings in ent.Comp.Markings.Values)
        {
            foreach (var marking in markings)
            {
                yield return marking;
            }
        }

        var censorNudity = _cfg.GetCVar(CCVars.AccessibilityClientCensorNudity) || _cfg.GetCVar(CCVars.AccessibilityServerCensorNudity);
        if (!censorNudity)
            yield break;

        var group = ProtoMan.Index(ent.Comp.MarkingData.Group);
        foreach (var layer in ent.Comp.MarkingData.Layers)
        {
            if (!group.Limits.TryGetValue(layer, out var layerLimits))
                continue;

            if (layerLimits.NudityDefault.Count < 1)
                continue;

            var markings = ent.Comp.Markings.GetValueOrDefault(layer) ?? [];
            if (markings.Any(marking => _marking.TryGetMarking(marking, out var proto) && proto.BodyPart == layer))
                continue;

            foreach (var marking in layerLimits.NudityDefault)
            {
                yield return new(marking, 1);
            }
        }
    }

    /// <summary>
    ///     Adds the markings associated with a specific organ to the sprite layers of a body.
    /// </summary>
    /// <param name="ent">An organ with markings applied to it.</param>
    /// <param name="target">The visual body entity containing this organ.</param>
    private void ApplyMarkings(Entity<VisualOrganMarkingsComponent> ent, Entity<SpriteComponent?> target)
    {
        // <Onyx-DetachedOrganMarkings-edited>
        if (!ent.Comp.ShowOnDetached &&
            TryComp<OrganComponent>(ent.Owner, out var organ) &&
            organ.Body == null)
        {
            ent.Comp.AppliedMarkings.Clear();
            return;
        }
        // </Onyx-DetachedOrganMarkings-edited>

        if (!Resolve(target, ref target.Comp))
            return;

        var applied = new List<Marking>();
        foreach (var marking in AllMarkings(ent))
        {
            if (!_marking.TryGetMarking(marking, out var proto))
                continue;

            // <Onyx-DetachedPartVisuals-edited>
            var organIndex = _sprite.LayerMapTryGet(target, proto.BodyPart, out var existing, false)
                ? existing
                : _sprite.LayerMapReserve(target, proto.BodyPart);
            // </Onyx-DetachedPartVisuals-edited>

            // Get displacement data for this marking, if there is any
            ent.Comp.MarkingsDisplacement.TryGetValue(proto.BodyPart, out var displacement);

            // Add the marking's layers to the target's sprite
            ApplyMarkingLayers(target, organIndex, marking, proto, displacement);
            applied.Add(marking);
        }

        ent.Comp.AppliedMarkings = applied;
    }

    /// <summary>
    ///     Add sprite layers over a sprite layer of the target entity from a marking prototype.
    /// </summary>
    /// <param name="target">The entity to apply the marking.</param>
    /// <param name="baseLayerIndex">The index of the base layer on the entity's sprite stack.</param>
    /// <param name="markingConfig">The marking's configuration data.</param>
    /// <param name="markingProto">The marking prototype to add.</param>
    /// <param name="displacement">Optional displacement data associated with this marking.</param>
    private void ApplyMarkingLayers(Entity<SpriteComponent?> target,
        int baseLayerIndex,
        Marking markingConfig,
        MarkingPrototype markingProto,
        DisplacementData? displacement = null)
    {
        if (!Resolve(target, ref target.Comp) || !_sprite.LayerExists(target, baseLayerIndex))
            return;

        var numDisplacements = 0;
        for (var markingLayer = 0; markingLayer < markingProto.Sprites.Count; markingLayer++)
        {
            ApplyMarkingLayer(
                target,
                markingLayer,
                baseLayerIndex,
                markingConfig,
                markingProto,
                ref numDisplacements,
                displacement);
        }
    }

    /// <summary>
    ///     Applies a single layer of a marking prototype on a target's sprite layer stack, adding the layer if needed.
    /// </summary>
    /// <param name="target">The target entity.</param>
    /// <param name="markingLayer">The index of the marking layer to add.</param>
    /// <param name="baseLayerIndex">The sprite layer index of the base layer to apply the marking to.</param>
    /// <param name="markingConfig">The marking's configuration data.</param>
    /// <param name="markingProto">The marking prototype.</param>
    /// <param name="numDisplacements">How many displacements have been applied so far.</param>
    /// <param name="displacement">Displacement data associated with this marking.</param>
    private void ApplyMarkingLayer(Entity<SpriteComponent?> target,
        int markingLayer,
        int baseLayerIndex,
        Marking markingConfig,
        MarkingPrototype markingProto,
        ref int numDisplacements,
        DisplacementData? displacement = null)
    {
        if (!Resolve(target, ref target.Comp)
            || !markingProto.Sprites.TryGetValue(markingLayer, out var layerData)
            || !_sprite.TryGetLayer(target, baseLayerIndex, out var baseLayer, true))
            return;

        // - The +1 ensures that marking layers are added on top of the base organ.
        var spriteLayerIndex = baseLayerIndex + markingLayer + 1;

        // Add the marking layer to the target entity, if the target doesn't have it yet
        var spriteLayer = EnsureTargetLayer(
            target,
            markingProto,
            layerData,
            newLayerIndex: spriteLayerIndex + numDisplacements,
            visible: baseLayer.Visible);

        UpdateLayerColor(target, markingConfig, markingLayer, spriteLayer);

        // Apply displacements
        if (displacement != null && markingProto.CanBeDisplaced)
        {
            var layerId = layerData.GetLayerID(markingProto.ID);
            var spriteTarget = (target.Owner, target.Comp);

            _displacement.TryAddDisplacement(
                displacement,
                spriteTarget,
                // Similar logic as above, but this makes the displacement layer go below the
                // original sprite. So it should be all the displacements, then all the sprite layers on top
                spriteLayerIndex,
                layerId,
                out _
            );

            numDisplacements++;
        }

        // Apply shaders
        if (layerData.Shader != null)
        {
            // TODO: fix this when LayerSetShader is moved out of component
            target.Comp.LayerSetShader(spriteLayerIndex + numDisplacements, layerData.Shader);
        }
    }

    /// <summary>
    ///     Gets the target layer off of an entity, otherwise adds the marking layer
    ///     to the target entity, if the target doesn't have it yet
    /// </summary>
    /// <param name="target">The target entity.</param>
    /// <param name="markingProto">The marking prototype.</param>
    /// <param name="layerData">The data associated with this layer.</param>
    /// <param name="newLayerIndex">The index that this layer should be found in.</param>
    /// <param name="visible">Whether or not this layer should be visible.</param>
    /// <returns>The index of the sprite layer associated with this marking layer.</returns>
    private int EnsureTargetLayer(Entity<SpriteComponent?> target,
        MarkingPrototype markingProto,
        MarkingLayerData layerData,
        int newLayerIndex,
        bool visible)
    {
        var layerId = layerData.GetLayerID(markingProto.ID);
        var sprite = layerData.Sprite;

        if (!_sprite.LayerMapTryGet(target, layerId, out var spriteLayer, false))
        {
            spriteLayer = _sprite.AddLayer(target, sprite, newLayerIndex);
            _sprite.LayerMapSet(target, layerId, spriteLayer);
            EnsureComp<MarkingLayersComponent>(target.Owner).LayerIds.Add(layerId); // <Onyx-MarkingBounds>
            _sprite.LayerSetSprite(target, layerId, sprite);
            _sprite.LayerSetVisible(target, spriteLayer, visible);
        }

        return spriteLayer;
    }

    /// <summary>
    ///     Sets the color of a layer according to marking preference data.
    /// </summary>
    /// <param name="target">The target entity.</param>
    /// <param name="markingConfig">The marking's configuration data.</param>
    /// <param name="markingLayer">The index of the layer in the marking prototype.</param>
    /// <param name="spriteLayer">The index of the sprite layer associated with this marking layer.</param>
    private void UpdateLayerColor(Entity<SpriteComponent?> target,
        Marking markingConfig,
        int markingLayer,
        int spriteLayer)
    {
        var layerColor = markingLayer < markingConfig.MarkingColors?.Count
            ? markingConfig.MarkingColors[markingLayer]
            : Color.White;

        _sprite.LayerSetColor(target, spriteLayer, layerColor);
    }

    /// <summary>
    ///     Removes the sprite layers for every marking applied to an organ.
    /// </summary>
    /// <param name="ent">An organ with markings applied to it.</param>
    /// <param name="target">The visual body of the entity.</param>
    private void RemoveMarkings(Entity<VisualOrganMarkingsComponent> ent, Entity<SpriteComponent?> target)
    {
        if (!Resolve(target, ref target.Comp))
            return;

        foreach (var marking in ent.Comp.AppliedMarkings)
        {
            if (!_marking.TryGetMarking(marking, out var proto))
                continue;

            RemoveMarkingSpriteLayers(target, proto);
        }
    }

    /// <summary>
    ///     Removes all sprite layers associated with a certain marking prototype from an entity.
    /// </summary>
    /// <param name="target">The target entity.</param>
    /// <param name="proto">The marking prototype.</param>
    private void RemoveMarkingSpriteLayers(Entity<SpriteComponent?> target, MarkingPrototype proto)
    {
        if (!Resolve(target, ref target.Comp))
            return;

        foreach (var layer in proto.Sprites)
        {
            var layerId = layer.GetLayerID(proto.ID);
            RemoveMarkingSprite(target, layerId, proto);
        }
    }

    /// <summary>
    ///     Removes a single layer associated with a marking prototype from an entity.
    /// </summary>
    /// <param name="target">The target entity.</param>
    /// <param name="layerId">The ID of the layer to remove.</param>
    /// <param name="proto">THe marking prototype.</param>
    private void RemoveMarkingSprite(Entity<SpriteComponent?> target, string layerId, MarkingPrototype proto)
    {
        if (!Resolve(target, ref target.Comp))
            return;

        // If this marking is one that can be displaced, we need to remove the displacement as well; otherwise
        // altering a marking at runtime can lead to the renderer falling over.
        // The Vulps must be shaved.
        // (https://github.com/space-wizards/space-station-14/issues/40135).
        if (proto.CanBeDisplaced)
            _displacement.EnsureDisplacementIsNotOnSprite((target, target.Comp), layerId);

        if (!_sprite.LayerMapTryGet(target, layerId, out var index, false))
            return;

        _sprite.LayerMapRemove(target, layerId);
        _sprite.RemoveLayer(target, index);
        // <Onyx-MarkingBounds>
        if (TryComp<MarkingLayersComponent>(target.Owner, out var markingLayers))
        {
            markingLayers.LayerIds.Remove(layerId);
            if (markingLayers.LayerIds.Count == 0)
                RemComp<MarkingLayersComponent>(target.Owner);
        }
        // </Onyx-MarkingBounds>
    }

    private void OnMarkingsChangedVisibility(Entity<VisualOrganMarkingsComponent> ent, ref BodyRelayedEvent<HumanoidLayerVisibilityChangedEvent> args)
    {
        if (!ent.Comp.HideableLayers.Contains(args.Args.Layer))
            return;

        // This hurts.
        args.Args = args.Args with { ShouldHide = true };

        foreach (var markings in ent.Comp.Markings.Values)
        {
            foreach (var marking in markings)
            {
                if (!_marking.TryGetMarking(marking, out var proto))
                    continue;

                if (proto.BodyPart != args.Args.Layer
                    && !(ent.Comp.DependentHidingLayers.TryGetValue(args.Args.Layer, out var dependent)
                        && dependent.Contains(proto.BodyPart)))
                    continue;

                var bodyEnt = args.Body.Owner;
                var visible = args.Args.Visible;
                UpdateMarkingLayerVisibility(proto, bodyEnt, visible);
            }
        }
    }

    /// <summary>
    ///     Update the visibility of a marking prototype.
    /// </summary>
    /// <param name="proto">The marking prototype.</param>
    /// <param name="body">The entity with this marking.</param>
    /// <param name="visible">The visibility of this marking.</param>
    private void UpdateMarkingLayerVisibility(MarkingPrototype proto, EntityUid body, bool visible)
    {
        foreach (var layer in proto.Sprites)
        {
            var layerId = layer.GetLayerID(proto.ID);

            // Not logging, can be called on initialization before the body's sprites are setup!
            if (_sprite.LayerMapTryGet(body, layerId, out var index, logMissing: false))
                _sprite.LayerSetVisible(body, index, visible);
        }
    }
}
