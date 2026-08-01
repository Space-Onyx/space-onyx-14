using System.Linq;
using Content.Client.DisplacementMap;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client.Body;

public sealed partial class VisualBodySystem : SharedVisualBodySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DisplacementMapSystem _displacement = default!;
    [Dependency] private MarkingManager _marking = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisualOrganComponent, OrganGotInsertedEvent>(OnOrganGotInserted);
        SubscribeLocalEvent<VisualOrganComponent, OrganGotRemovedEvent>(OnOrganGotRemoved);
        SubscribeLocalEvent<VisualOrganComponent, AfterAutoHandleStateEvent>(OnOrganState);

        SubscribeLocalEvent<VisualOrganMarkingsComponent, OrganGotInsertedEvent>(OnMarkingsGotInserted);
        SubscribeLocalEvent<VisualOrganMarkingsComponent, OrganGotRemovedEvent>(OnMarkingsGotRemoved);
        SubscribeLocalEvent<VisualOrganMarkingsComponent, AfterAutoHandleStateEvent>(OnMarkingsState);

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
    }

    private void OnOrganGotRemoved(Entity<VisualOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        RemoveVisual(ent, args.Target);
        // <Onyx-DetachedPartVisuals-edited>
        var detachedRoot = GetDetachedPartRoot(ent.Owner);
        if (detachedRoot == ent.Owner && HasComp<Content.Shared.Body.Part.BodyPartComponent>(ent.Owner))
            ClearDetachedBodyPartVisuals(detachedRoot);

        ApplyVisual(ent, detachedRoot);
        // </Onyx-DetachedPartVisuals-edited>
    }

    private void OnOrganState(Entity<VisualOrganComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyVisual(ent, GetVisualTarget(ent));
    }

    private void ApplyVisual(Entity<VisualOrganComponent> ent, EntityUid target)
    {
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
    }

    private void OnMarkingsGotRemoved(Entity<VisualOrganMarkingsComponent> ent, ref OrganGotRemovedEvent args)
    {
        RemoveMarkings(ent, args.Target);
        // <Onyx-DetachedPartVisuals-edited>
        ApplyMarkings(ent, GetDetachedPartRoot(ent.Owner));
        // </Onyx-DetachedPartVisuals-edited>
    }

    private void OnMarkingsState(Entity<VisualOrganMarkingsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var body = GetVisualTarget(ent);
        RemoveMarkings(ent, body);
        ApplyMarkings(ent, body);
    }

    protected override void SetOrganColor(Entity<VisualOrganComponent> ent, Color color)
    {
        base.SetOrganColor(ent, color);

        ApplyVisual(ent, GetVisualTarget(ent));
    }

    protected override void SetOrganMarkings(Entity<VisualOrganMarkingsComponent> ent, Dictionary<HumanoidVisualLayers, List<Marking>> markings)
    {
        base.SetOrganMarkings(ent, markings);

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

    protected override void SetOrganAppearance(Entity<VisualOrganComponent> ent, PrototypeLayerData data)
    {
        base.SetOrganAppearance(ent, data);

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

            var index = _sprite.LayerMapTryGet(target, proto.BodyPart, out var existing, false)
                ? existing
                : _sprite.LayerMapReserve(target, proto.BodyPart);

            ent.Comp.MarkingsDisplacement.TryGetValue(proto.BodyPart, out var displacement);

            var numDisplacements = 0;
            for (var i = 0; i < proto.Sprites.Count; i++)
            {
                var sprite = proto.Sprites[i];

                DebugTools.Assert(sprite is SpriteSpecifier.Rsi);
                if (sprite is not SpriteSpecifier.Rsi rsi)
                    continue;

                var layerId = $"{proto.ID}-{rsi.RsiState}";

                if (!_sprite.LayerMapTryGet(target, layerId, out _, false))
                {
                    // Having three separate indices and a magic +1 is cursed, but:
                    // - index refers to the index of the organ the marking is applied to
                    // - i is the current sprite of the marking that is being applied
                    // - numDisplacements tracks how many displacements have been applied, and is
                    //   an additional offset to ensure that the order of the base sprites is correct
                    //   after inserting a displacement layer
                    // - The +1 ensures that markings render on top of the base organ
                    var spriteLayer = _sprite.AddLayer(target, sprite, index + i + numDisplacements + 1);
                    _sprite.LayerMapSet(target, layerId, spriteLayer);
                    _sprite.LayerSetSprite(target, layerId, rsi);
                }

                if (marking.MarkingColors is not null && i < marking.MarkingColors.Count)
                    _sprite.LayerSetColor(target, layerId, marking.MarkingColors[i]);
                else
                    _sprite.LayerSetColor(target, layerId, Color.White);

                if (displacement != null && proto.CanBeDisplaced)
                {
                    _displacement.TryAddDisplacement(
                        displacement,
                        (target, target.Comp),
                        // Similar logic as above, but this makes the displacement layer go below the
                        // original sprite. So it should be all the displacements, then all the sprite layers on top
                        index + i + 1,
                        layerId,
                        out _
                    );
                    numDisplacements++;
                }

                if (proto.Shaders is not null &&
                    proto.Shaders.TryGetValue(rsi.RsiState, out var shader))
                {
                    // TODO: fix this when LayerSetShader is moved out of component
                    target.Comp.LayerSetShader(index + i + 1 + numDisplacements, shader);
                }
            }

            applied.Add(marking);
        }
        ent.Comp.AppliedMarkings = applied;
    }

    private void RemoveMarkings(Entity<VisualOrganMarkingsComponent> ent, Entity<SpriteComponent?> target)
    {
        if (!Resolve(target, ref target.Comp))
            return;

        foreach (var marking in ent.Comp.AppliedMarkings)
        {
            if (!_marking.TryGetMarking(marking, out var proto))
                continue;

            foreach (var sprite in proto.Sprites)
            {
                DebugTools.Assert(sprite is SpriteSpecifier.Rsi);
                if (sprite is not SpriteSpecifier.Rsi rsi)
                    continue;

                var layerId = $"{proto.ID}-{rsi.RsiState}";

                // If this marking is one that can be displaced, we need to remove the displacement as well; otherwise
                // altering a marking at runtime can lead to the renderer falling over.
                // The Vulps must be shaved.
                // (https://github.com/space-wizards/space-station-14/issues/40135).
                if (proto.CanBeDisplaced)
                    _displacement.EnsureDisplacementIsNotOnSprite((target, target.Comp), layerId);

                if (!_sprite.LayerMapTryGet(target, layerId, out var index, false))
                    continue;

                _sprite.LayerMapRemove(target, layerId);
                _sprite.RemoveLayer(target, index);
            }
        }
    }

    private void OnMarkingsChangedVisibility(Entity<VisualOrganMarkingsComponent> ent, ref BodyRelayedEvent<HumanoidLayerVisibilityChangedEvent> args)
    {
        if (!ent.Comp.HideableLayers.Contains(args.Args.Layer))
            return;

        foreach (var markings in ent.Comp.Markings.Values)
        {
            foreach (var marking in markings)
            {
                if (!_marking.TryGetMarking(marking, out var proto))
                    continue;

                if (proto.BodyPart != args.Args.Layer && !(ent.Comp.DependentHidingLayers.TryGetValue(args.Args.Layer, out var dependent) && dependent.Contains(proto.BodyPart)))
                    continue;

                foreach (var sprite in proto.Sprites)
                {
                    DebugTools.Assert(sprite is SpriteSpecifier.Rsi);
                    if (sprite is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var layerId = $"{proto.ID}-{rsi.RsiState}";

                    if (!_sprite.LayerMapTryGet(args.Body.Owner, layerId, out var index, true))
                        continue;

                    _sprite.LayerSetVisible(args.Body.Owner, index, args.Args.Visible);
                }
            }
        }
    }
}
