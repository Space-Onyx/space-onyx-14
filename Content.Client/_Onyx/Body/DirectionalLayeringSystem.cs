// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Body;
using Content.Client.Clothing;
using Content.Client.Eye;
using Content.Client.Inventory;
using Content.Shared.Body;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;

namespace Content.Client._Onyx.Body;

/// <summary>
/// Orders complete organ, marking and clothing blocks against the rendered direction, including preview overrides.
/// </summary>
public sealed partial class DirectionalLayeringSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private MarkingManager _marking = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private readonly Dictionary<EntityUid, Layout> _layouts = new();
    private EntityQuery<VisualOrganMarkingsComponent> _markingsQuery;

    public override void Initialize()
    {
        base.Initialize();
        _markingsQuery = GetEntityQuery<VisualOrganMarkingsComponent>();
        SubscribeLocalEvent<VisualBodyComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EquipmentVisualsUpdatedEvent>(OnEquipmentUpdated);
        UpdatesAfter.Add(typeof(TransformSystem));
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
        UpdatesAfter.Add(typeof(VisualBodySystem));
        UpdatesAfter.Add(typeof(ClientClothingSystem));
    }

    public override void Shutdown()
    {
        _layouts.Clear();
        base.Shutdown();
    }

    private void OnShutdown(Entity<VisualBodyComponent> ent, ref ComponentShutdown args) =>
        _layouts.Remove(ent.Owner);

    private void OnEquipmentUpdated(EquipmentVisualsUpdatedEvent args) => Invalidate(args.Equipee);

    /// <summary>
    /// Invalidates block membership after the native visual system changes an organ or its markings.
    /// </summary>
    public void Invalidate(EntityUid uid)
    {
        if (_layouts.TryGetValue(uid, out var layout))
            layout.Dirty = true;
    }

    /// <summary>
    /// Applies ordering using the same four-direction lookup as the renderer. Does not retain UI direction overrides.
    /// </summary>
    public bool TryUpdate(Entity<SpriteComponent?> ent, Angle rotation, Direction? direction = null)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !HasComp<VisualBodyComponent>(ent))
            return false;

        var rendered = direction == null
            ? SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, rotation.Reduced().FlipPositive())
            : direction.Value.Convert(RsiDirectionType.Dir4);
        if (!_layouts.TryGetValue(ent.Owner, out var layout) || layout.Sprite != ent.Comp)
        {
            layout = new Layout(ent.Comp);
            _layouts[ent.Owner] = layout;
        }

        // Native systems may rebuild layers without changing facing (equipment, surgery, profile reloads).
        if (!layout.Dirty && layout.Direction == rendered && Matches(ent.Comp.LayerData, layout.Applied))
            return true;

        Rebuild(ent, layout);
        var maskLowered = _inventory.TryGetSlotEntity(ent.Owner, "mask", out var mask) &&
                          TryComp<MaskComponent>(mask, out var maskComponent) && maskComponent.IsToggled;
        Order(layout, rendered, maskLowered);
        Apply(ent, layout);
        layout.Direction = rendered;
        layout.Dirty = false;
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<VisualBodyComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var transform))
        {
            if (transform.MapID == MapId.Nullspace || !sprite.Visible || sprite.ContainerOccluded)
                continue;

            TryUpdate((uid, sprite), _transform.GetWorldRotation(transform) + _eye.CurrentEye.Rotation);
        }
    }

    private void Rebuild(Entity<SpriteComponent?> ent, Layout layout)
    {
        layout.Groups.Clear();
        layout.Keys.Clear();
        var layers = ent.Comp!.LayerData;
        layout.Native.RemoveAll(layer => IndexOf(layers, layer) < 0);
        for (var i = 0; i < layers.Count; i++)
        {
            if (layout.Native.Contains(layers[i]))
                continue;

            var previous = i == 0 ? -1 : layout.Native.IndexOf(layers[i - 1]);
            layout.Native.Insert(previous + 1, layers[i]);
        }
        layout.Order.Clear();
        layout.Order.AddRange(layout.Native);

        foreach (var layer in Enum.GetValues<HumanoidVisualLayers>())
            AddKey(ent, layout, layer, GetGroup(layout, layer));

        foreach (var organ in _body.EnumerateOrgans<VisualOrganMarkingsComponent>(ent.Owner, _markingsQuery))
        {
            foreach (var marking in organ.Comp2.AppliedMarkings)
            {
                if (!_marking.TryGetMarking(marking, out var prototype))
                    continue;

                var group = GetGroup(layout, prototype.BodyPart);
                foreach (var layer in prototype.Sprites)
                    AddKey(ent, layout, layer.GetLayerID(prototype.ID), group);
            }
        }

        if (TryComp(ent, out InventorySlotsComponent? slots))
        {
            foreach (var (slot, keys) in slots.VisualLayerKeys)
            {
                var group = GetGroup(layout, slot);
                AddKey(ent, layout, slot, group);
                foreach (var key in keys)
                    AddKey(ent, layout, key, group);
            }
        }

        // Empty slot bookmarks exist before InventorySlots has rendered any equipment.
        foreach (var slot in new[] { "neck", "head", "mask", "maskalt", "back" })
            AddKey(ent, layout, slot, GetGroup(layout, slot));
    }

    private void AddKey(Entity<SpriteComponent?> ent, Layout layout, object key, List<SpriteComponent.Layer> group)
    {
        if (!TryGetIndex(ent, key, out var index))
            return;

        var layer = ent.Comp!.LayerData[index];
        layout.Keys[key] = layer;
        if (!group.Contains(layer))
            group.Add(layer);

        // Displacement textures must continue to render immediately before their consumers.
        var displacement = $"{key}-displacement";
        if (_sprite.LayerMapTryGet(ent, displacement, out index, false))
        {
            layer = ent.Comp.LayerData[index];
            layout.Keys[displacement] = layer;
            if (!group.Contains(layer))
                group.Add(layer);
        }
    }

    private static List<SpriteComponent.Layer> GetGroup(Layout layout, object key)
    {
        if (!layout.Groups.TryGetValue(key, out var group))
        {
            group = new();
            layout.Groups.Add(key, group);
        }

        return group;
    }

    private static void Order(Layout layout, RsiDirection direction, bool maskLowered)
    {
        var hair = GetGroup(layout, HumanoidVisualLayers.Hair);
        var trim = new List<SpriteComponent.Layer>();
        trim.AddRange(GetGroup(layout, HumanoidVisualLayers.HeadSide));
        trim.AddRange(GetGroup(layout, HumanoidVisualLayers.HeadTop));
        var tail = new List<SpriteComponent.Layer>();
        tail.AddRange(GetGroup(layout, HumanoidVisualLayers.Tail));
        tail.AddRange(GetGroup(layout, HumanoidVisualLayers.TailOverlay));
        var neck = GetGroup(layout, "neck");
        var mask = GetGroup(layout, "mask");

        MoveRelative(layout.Order, hair, trim, direction == RsiDirection.North);
        MoveRelative(layout.Order, mask, hair, !maskLowered);
        MoveRelative(layout.Order, GetGroup(layout, "maskalt"), hair, false);

        if (direction == RsiDirection.North)
        {
            var headTrim = new List<SpriteComponent.Layer>(trim);
            headTrim.AddRange(hair);
            if (!maskLowered)
                headTrim.AddRange(mask);
            MoveRelative(layout.Order, tail, headTrim, true);
            MoveRelative(layout.Order, neck, tail, false);
        }
        else
        {
            // Clothing's "head" bookmark is distinct from the actual Head organ layer.
            MoveRelative(layout.Order, neck, GetGroup(layout, "head"), false);
            if (direction == RsiDirection.South)
                MoveTo(layout.Order, tail, 0);
            else
            {
                var legs = new List<SpriteComponent.Layer>();
                foreach (var key in new[] { HumanoidVisualLayers.RLeg, HumanoidVisualLayers.LLeg,
                             HumanoidVisualLayers.RFoot, HumanoidVisualLayers.LFoot })
                    legs.AddRange(GetGroup(layout, key));
                MoveRelative(layout.Order, tail, legs, false);
            }
        }

        // Reorder real limb blocks, including markings, rather than rendering duplicate animated layers.
        if (direction is RsiDirection.East or RsiDirection.West)
        {
            var rightInFront = direction == RsiDirection.East;
            MoveRelative(layout.Order,
                GetGroup(layout, rightInFront ? HumanoidVisualLayers.RLeg : HumanoidVisualLayers.LLeg),
                GetGroup(layout, rightInFront ? HumanoidVisualLayers.LLeg : HumanoidVisualLayers.RLeg), true);
            MoveRelative(layout.Order,
                GetGroup(layout, rightInFront ? HumanoidVisualLayers.RFoot : HumanoidVisualLayers.LFoot),
                GetGroup(layout, rightInFront ? HumanoidVisualLayers.LFoot : HumanoidVisualLayers.RFoot), true);
        }
    }

    private static void MoveRelative(List<SpriteComponent.Layer> order, List<SpriteComponent.Layer> block,
        List<SpriteComponent.Layer> anchor, bool above)
    {
        if (block.Count == 0 || anchor.Count == 0)
            return;

        // Resolve the anchor after excluding the block; subtracting block.Count from its index is incorrect.
        var index = above ? -1 : order.Count;
        var remaining = 0;
        var found = false;
        foreach (var layer in order)
        {
            if (block.Contains(layer))
                continue;
            if (anchor.Contains(layer))
            {
                index = above ? remaining + 1 : Math.Min(index, remaining);
                found = true;
            }
            remaining++;
        }

        if (found)
            MoveTo(order, block, index);
    }

    private static void MoveTo(List<SpriteComponent.Layer> order, List<SpriteComponent.Layer> block, int index)
    {
        // Keep native relative order, including shader producers, across split marking rebuilds.
        block.Sort((a, b) => order.IndexOf(a).CompareTo(order.IndexOf(b)));
        foreach (var layer in block)
            order.Remove(layer);
        order.InsertRange(index, block);
    }

    private void Apply(Entity<SpriteComponent?> ent, Layout layout)
    {
        for (var i = 0; i < layout.Order.Count; i++)
        {
            while (ent.Comp!.LayerData[i] != layout.Order[i])
            {
                var layer = layout.Order[i];
                var target = i;
                // Never remove an unrelated layer: its private engine mappings cannot be enumerated by content.
                if (!layout.Keys.ContainsValue(layer))
                {
                    layer = ent.Comp.LayerData[i];
                    target = layout.Order.IndexOf(layer);
                }

                var current = IndexOf(ent.Comp.LayerData, layer);
                _sprite.RemoveLayer(ent, current, out _, false);
                _sprite.AddLayer(ent, layer, target);
                foreach (var (key, mapped) in layout.Keys)
                {
                    if (mapped != layer)
                        continue;
                    if (key is Enum enumKey)
                        _sprite.LayerMapSet(ent, enumKey, target);
                    else if (key is string stringKey)
                        _sprite.LayerMapSet(ent, stringKey, target);
                }
            }
        }

        layout.Applied.Clear();
        layout.Applied.AddRange(layout.Order);
    }

    private bool TryGetIndex(Entity<SpriteComponent?> ent, object key, out int index)
    {
        index = -1;
        return key switch
        {
            Enum enumKey => _sprite.LayerMapTryGet(ent, enumKey, out index, false),
            string stringKey => _sprite.LayerMapTryGet(ent, stringKey, out index, false),
            _ => false,
        };
    }

    private static int IndexOf(IReadOnlyList<SpriteComponent.Layer> layers, SpriteComponent.Layer layer)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            if (layers[i] == layer)
                return i;
        }

        return -1;
    }

    private static bool Matches(IReadOnlyList<SpriteComponent.Layer> layers, List<SpriteComponent.Layer> applied)
    {
        if (layers.Count != applied.Count)
            return false;
        for (var i = 0; i < layers.Count; i++)
        {
            if (layers[i] != applied[i])
                return false;
        }

        return true;
    }

    private sealed class Layout(SpriteComponent sprite)
    {
        public readonly SpriteComponent Sprite = sprite;
        public readonly Dictionary<object, List<SpriteComponent.Layer>> Groups = new();
        public readonly Dictionary<object, SpriteComponent.Layer> Keys = new();
        public readonly List<SpriteComponent.Layer> Order = new();
        public readonly List<SpriteComponent.Layer> Native = new();
        public readonly List<SpriteComponent.Layer> Applied = new();
        public RsiDirection Direction;
        public bool Dirty = true;
    }
}
