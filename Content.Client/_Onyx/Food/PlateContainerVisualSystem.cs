using System.Numerics;
using System.Linq;
using Content.Client.Items.Systems;
using Content.Shared._Onyx.Food.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._Onyx.Food;

public sealed partial class PlateContainerVisualSystem : EntitySystem
{
    private const string LayerPrefix = "onyx-plate-content-";

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemSystem _item = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, List<string>> _layers = new();
    private readonly HashSet<EntityUid> _queued = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlateContainerComponent, ComponentStartup>(OnPlateStartup);
        SubscribeLocalEvent<PlateContainerComponent, ComponentShutdown>(OnPlateShutdown);
        SubscribeLocalEvent<PlateContainerComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<PlateContainerComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<SpriteComponent, AppearanceChangeEvent>(OnSpriteAppearanceChanged);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var plate in _queued)
        {
            if (TryComp(plate, out PlateContainerComponent? component))
                Rebuild((plate, component));
        }

        _queued.Clear();
    }

    private void OnPlateStartup(Entity<PlateContainerComponent> ent, ref ComponentStartup args)
    {
        _queued.Add(ent.Owner);
    }

    private void OnPlateShutdown(Entity<PlateContainerComponent> ent, ref ComponentShutdown args)
    {
        ClearLayers(ent.Owner);
        _queued.Remove(ent.Owner);
    }

    private void OnContainerChanged(Entity<PlateContainerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == PlateContainerComponent.ContainerId)
            _queued.Add(ent.Owner);
    }

    private void OnContainerChanged(Entity<PlateContainerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == PlateContainerComponent.ContainerId)
            _queued.Add(ent.Owner);
    }

    private void OnSpriteAppearanceChanged(Entity<SpriteComponent> ent, ref AppearanceChangeEvent args)
    {
        QueueContainingPlate(ent.Owner);
    }

    private void QueueContainingPlate(EntityUid item)
    {
        if (!_container.TryGetContainingContainer(item, out var container) ||
            container.ID != PlateContainerComponent.ContainerId ||
            !HasComp<PlateContainerComponent>(container.Owner))
        {
            return;
        }

        _queued.Add(container.Owner);
    }

    private void Rebuild(Entity<PlateContainerComponent> ent)
    {
        ClearLayers(ent.Owner);

        if (!TryComp(ent.Owner, out SpriteComponent? plateSprite) ||
            !_container.TryGetContainer(
                ent.Owner,
                PlateContainerComponent.ContainerId,
                out var container))
        {
            return;
        }

        var keys = new List<string>();

        for (var itemIndex = 0; itemIndex < container.ContainedEntities.Count; itemIndex++)
        {
            var item = container.ContainedEntities[itemIndex];
            if (!TryComp(item, out SpriteComponent? itemSprite))
                continue;

            var itemOffset = itemIndex < ent.Comp.ItemOffsets.Count
                ? ent.Comp.ItemOffsets[itemIndex]
                : Vector2.Zero;

            var sourceLayerCount = itemSprite.AllLayers.Count();
            for (var sourceIndex = 0; sourceIndex < sourceLayerCount; sourceIndex++)
            {
                if (itemSprite[sourceIndex] is not SpriteComponent.Layer source)
                    continue;

                if (!source.Visible || source.Blank)
                    continue;

                var key = $"{LayerPrefix}{itemIndex}-{sourceIndex}";
                var targetIndex = _sprite.LayerMapReserve((ent.Owner, plateSprite), key);
                var data = CreateLayerData(itemOffset, itemSprite, source);

                _sprite.LayerSetData((ent.Owner, plateSprite), targetIndex, data);
                if (!source.State.IsValid && source.Texture != null)
                    _sprite.LayerSetTexture((ent.Owner, plateSprite), targetIndex, source.Texture);

                keys.Add(key);
            }
        }

        if (keys.Count > 0)
            _layers[ent.Owner] = keys;
        _item.VisualsChanged(ent.Owner);
    }

    private static PrototypeLayerData CreateLayerData(
        Vector2 itemOffset,
        SpriteComponent itemSprite,
        SpriteComponent.Layer source)
    {
        return new PrototypeLayerData
        {
            RsiPath = source.State.IsValid
                ? source.ActualRsi?.Path.ToString()
                : null,
            State = source.State.IsValid
                ? source.State.Name
                : null,
            Scale = itemSprite.Scale * source.Scale,
            Rotation = itemSprite.Rotation + source.Rotation,
            Offset = itemOffset + itemSprite.Offset + source.Offset,
            Visible = true,
            Color = itemSprite.Color * source.Color,
            RenderingStrategy = source.RenderingStrategy,
            Cycle = source.Cycle,
        };
    }

    private void ClearLayers(EntityUid plate)
    {
        if (!_layers.Remove(plate, out var keys) ||
            !TryComp(plate, out SpriteComponent? sprite))
        {
            return;
        }

        foreach (var key in keys)
        {
            _sprite.RemoveLayer((plate, sprite), key, logMissing: false);
        }
    }
}
