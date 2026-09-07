// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Client.Items.Systems;
using Content.Shared._Onyx.Clothing;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using System.Numerics;
using System.Linq;

namespace Content.Client._Onyx.Clothing;

public sealed partial class ClothingDirtVisualizerSystem : EntitySystem
{
    private const string DirtShader = "OnyxDirtCoverage";
    private const string WorldLayerPrefix = "onyx-dirt-world-";
    private const string BodyLayerPrefix = "onyx-dirt-body-";

    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Dictionary<EntityUid, List<string>> _worldLayers = [];
    private readonly Dictionary<EntityUid, EntityUid> _bodyTargets = [];
    private readonly HashSet<EntityUid> _pending = [];
    private readonly HashSet<EntityUid> _bodyRebuild = [];
    private readonly Dictionary<(EntityUid Entity, string Layer), ShaderInstance> _shaders = [];
    private readonly Dictionary<EntityUid, HashSet<(EntityUid Entity, string Layer)>> _itemShaders = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothingDirtableComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ClothingDirtableComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ClothingDirtableComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<ClothingDirtableComponent, GetEquipmentVisualsEvent>(OnEquipment,
            after: [typeof(ClothingSystem)]);
        SubscribeLocalEvent<ClothingDirtableComponent, GetInhandVisualsEvent>(OnInhand,
            after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<ClothingDirtableComponent, OrganGotInsertedEvent>(OnPartRelationshipChanged);
        SubscribeLocalEvent<ClothingDirtableComponent, OrganGotRemovedEvent>(OnPartRelationshipChanged);
        SubscribeLocalEvent<ClothingDirtableComponent, BodyPartVisualChangedEvent>(OnBodyPartVisualChanged);
        SubscribeLocalEvent<ClothingDirtableComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentUpdated);
        SubscribeLocalEvent<ClothingDirtableComponent, HeldVisualsUpdatedEvent>(OnHeldUpdated);
    }

    public override void Shutdown()
    {
        foreach (var shader in _shaders.Values)
            shader.Dispose();
        _shaders.Clear();
        _itemShaders.Clear();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var uid in _pending)
        {
            if (!TryComp(uid, out ClothingDirtableComponent? dirtable))
                continue;
            if (TryComp(uid, out BodyPartComponent? part) && TryComp(uid, out VisualOrganComponent? visual))
                UpdateBodyPart((uid, dirtable), part, visual);
            else if (TryComp(uid, out SpriteComponent? sprite))
                UpdateWorldSprite((uid, dirtable), sprite);
        }
        _pending.Clear();
    }

    private void OnStartup(Entity<ClothingDirtableComponent> ent, ref ComponentStartup args)
        => _pending.Add(ent);

    private void OnRemove(Entity<ClothingDirtableComponent> ent, ref ComponentRemove args)
    {
        _pending.Remove(ent);
        _bodyRebuild.Remove(ent);
        ClearItemShaders(ent);
        ClearWorldLayers(ent);
        ClearBodyLayer(ent);
    }

    private void OnState(Entity<ClothingDirtableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ClearItemShaders(ent);
        _pending.Add(ent);
        _item.VisualsChanged(ent);
    }

    private void OnPartRelationshipChanged(Entity<ClothingDirtableComponent> ent, ref OrganGotInsertedEvent args)
        => QueueBodyRebuild(ent);

    private void OnPartRelationshipChanged(Entity<ClothingDirtableComponent> ent, ref OrganGotRemovedEvent args)
        => QueueBodyRebuild(ent);

    private void OnBodyPartVisualChanged(Entity<ClothingDirtableComponent> ent, ref BodyPartVisualChangedEvent args)
        => QueueBodyRebuild(ent);

    private void OnEquipment(Entity<ClothingDirtableComponent> ent, ref GetEquipmentVisualsEvent args)
        => AddMaskedLayers(ent.Comp, args.Layers);

    private void OnInhand(Entity<ClothingDirtableComponent> ent, ref GetInhandVisualsEvent args)
        => AddMaskedLayers(ent.Comp, args.Layers);

    private void OnEquipmentUpdated(Entity<ClothingDirtableComponent> ent, ref EquipmentVisualsUpdatedEvent args)
        => ApplyShaders(ent, args.Equipee, args.RevealedLayers, ent.Comp.DirtColor);

    private void OnHeldUpdated(Entity<ClothingDirtableComponent> ent, ref HeldVisualsUpdatedEvent args)
        => ApplyShaders(ent, args.User, args.RevealedLayers, ent.Comp.DirtColor);

    private static void AddMaskedLayers(ClothingDirtableComponent component, List<(string, PrototypeLayerData)> layers)
    {
        if (component.DirtColor is not { } dirt)
            return;

        var count = layers.Count;
        for (var i = 0; i < count; i++)
        {
            var (key, source) = layers[i];
            var copy = Copy(source);
            copy.MapKeys = null;
            copy.Color = Color.White;
            layers.Add(($"{key}-dirt", copy));
        }
    }

    private void UpdateWorldSprite(Entity<ClothingDirtableComponent> ent, SpriteComponent sprite)
    {
        if (ent.Comp.DirtColor is not { } dirt)
        {
            ClearWorldLayers(ent);
            return;
        }

        if (_worldLayers.TryGetValue(ent, out var existing))
        {
            foreach (var key in existing)
                UpdateShader((ent.Owner, sprite), key, dirt);
            return;
        }

        var keys = new List<string>();
        var sourceCount = sprite.AllLayers.Count();
        for (var i = 0; i < sourceCount; i++)
        {
            if (sprite[i] is not SpriteComponent.Layer source || !source.Visible || source.Blank)
                continue;

            var key = $"{WorldLayerPrefix}{i}";
            var index = _sprite.LayerMapReserve((ent.Owner, sprite), key);
            SetMaskedLayer((ent.Owner, sprite), index, source, dirt);
            ApplyShader((ent.Owner, sprite), index, key, dirt);
            keys.Add(key);
        }
        if (keys.Count > 0)
            _worldLayers[ent] = keys;
    }

    private void UpdateBodyPart(Entity<ClothingDirtableComponent> ent, BodyPartComponent part, VisualOrganComponent visual)
    {
        if (ent.Comp.DirtColor is not { } dirt)
        {
            ClearBodyLayer(ent);
            return;
        }

        if (!_bodyRebuild.Remove(ent) && _bodyTargets.TryGetValue(ent, out var existingTarget) &&
            TryComp(existingTarget, out SpriteComponent? existingSprite) &&
            _sprite.LayerMapTryGet((existingTarget, existingSprite), $"{BodyLayerPrefix}{ent.Owner}", out var existing, false))
        {
            UpdateShader((existingTarget, existingSprite), $"{BodyLayerPrefix}{ent.Owner}", dirt);
            return;
        }

        ClearBodyLayer(ent);

        var target = part.Body ?? ent.Owner;
        if (!TryComp(target, out SpriteComponent? sprite) ||
            !_sprite.LayerMapTryGet((target, sprite), visual.Layer, out var sourceIndex, false) ||
            sprite[sourceIndex] is not SpriteComponent.Layer source || source.Blank)
            return;

        var key = $"{BodyLayerPrefix}{ent.Owner}";
        var index = _sprite.AddLayer((target, sprite), new PrototypeLayerData(), sourceIndex + 1);
        _sprite.LayerMapSet((target, sprite), key, index);
        SetMaskedLayer((target, sprite), index, source, dirt);
        ApplyShader((target, sprite), index, key, dirt);
        _bodyTargets[ent] = target;
    }

    private void SetMaskedLayer(Entity<SpriteComponent?> target, int index, SpriteComponent.Layer source, Color dirt)
    {
        var data = new PrototypeLayerData
        {
            RsiPath = source.State.IsValid ? source.ActualRsi?.Path.ToString() : null,
            State = source.State.IsValid ? source.State.Name : null,
            Scale = source.Scale,
            Rotation = source.Rotation,
            Offset = source.Offset,
            Visible = source.Visible,
            Color = Color.White,
            RenderingStrategy = source.RenderingStrategy,
            Cycle = source.Cycle,
            Loop = source.Loop,
        };
        _sprite.LayerSetData(target, index, data);
        if (!source.State.IsValid && source.Texture != null)
            _sprite.LayerSetTexture(target, index, source.Texture);
    }

    private void ClearWorldLayers(EntityUid uid)
    {
        if (!_worldLayers.Remove(uid, out var keys) || !TryComp(uid, out SpriteComponent? sprite))
            return;
        foreach (var key in keys)
        {
            RemoveShader(uid, key);
            _sprite.RemoveLayer((uid, sprite), key, false);
        }
    }

    private void ClearBodyLayer(EntityUid part)
    {
        if (!_bodyTargets.Remove(part, out var target) || !TryComp(target, out SpriteComponent? sprite))
            return;
        var key = $"{BodyLayerPrefix}{part}";
        RemoveShader(target, key);
        _sprite.RemoveLayer((target, sprite), key, false);
    }

    private void QueueBodyRebuild(EntityUid part)
    {
        _bodyRebuild.Add(part);
        _pending.Add(part);
    }

    private void ApplyShaders(EntityUid item, EntityUid target, HashSet<string> layers, Color? dirt)
    {
        if (dirt is not { } color || !TryComp(target, out SpriteComponent? sprite))
            return;
        foreach (var key in layers)
        {
            if (!key.Contains("-dirt") || !_sprite.LayerMapTryGet((target, sprite), key, out var index, false))
                continue;
            ApplyShader((target, sprite), index, key, color);
            if (!_itemShaders.TryGetValue(item, out var itemShaders))
            {
                itemShaders = [];
                _itemShaders[item] = itemShaders;
            }
            itemShaders.Add((target, key));
        }
    }

    private void ApplyShader(Entity<SpriteComponent?> target, int index, string key, Color dirt)
    {
        RemoveShader(target, key);
        var shader = _prototypes.Index<ShaderPrototype>(DirtShader).InstanceUnique();
        SetShaderParameters(shader, dirt);
        _shaders[(target, key)] = shader;
        if (target.Comp != null)
            target.Comp.LayerSetShader(index, shader, DirtShader);
    }

    private void UpdateShader(Entity<SpriteComponent?> target, string key, Color dirt)
    {
        if (_shaders.TryGetValue((target, key), out var shader))
            SetShaderParameters(shader, dirt);
        else if (_sprite.LayerMapTryGet(target, key, out var index, false))
            ApplyShader(target, index, key, dirt);
    }

    private static void SetShaderParameters(ShaderInstance shader, Color dirt)
    {
        shader.SetParameter("coverage", dirt.A);
        shader.SetParameter("dirt_color", new Vector3(dirt.R, dirt.G, dirt.B));
    }

    private void RemoveShader(EntityUid target, string key)
    {
        if (_shaders.Remove((target, key), out var shader))
            shader.Dispose();
    }

    private void ClearItemShaders(EntityUid item)
    {
        if (!_itemShaders.Remove(item, out var keys))
            return;
        foreach (var (target, key) in keys)
            RemoveShader(target, key);
    }

    private static PrototypeLayerData Copy(PrototypeLayerData source)
        => new()
        {
            Shader = source.Shader,
            TexturePath = source.TexturePath,
            RsiPath = source.RsiPath,
            State = source.State,
            Scale = source.Scale,
            Rotation = source.Rotation,
            Offset = source.Offset,
            Visible = source.Visible,
            Color = source.Color,
            RenderingStrategy = source.RenderingStrategy,
            CopyToShaderParameters = source.CopyToShaderParameters,
            Cycle = source.Cycle,
            Loop = source.Loop,
        };
}
