using Content.Client.Items.Systems;
using Content.Shared._Onyx.Clothing;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using System.Linq;

namespace Content.Client._Onyx.Clothing;

public sealed partial class ClothingDirtVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedItemSystem _item = default!;

    private readonly Dictionary<EntityUid, TintState> _tints = [];
    private readonly HashSet<EntityUid> _pending = [];

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
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var uid in _pending)
        {
            if (TryComp(uid, out ClothingDirtableComponent? dirtable) && TryComp(uid, out SpriteComponent? sprite))
                UpdateSprite((uid, dirtable), sprite);
        }
        _pending.Clear();

        foreach (var (uid, tint) in _tints.ToArray())
        {
            if (!TryComp(uid, out ClothingDirtableComponent? dirtable) ||
                dirtable.DirtColor is not { } dirt ||
                !TryComp(uid, out SpriteComponent? sprite) ||
                sprite.Color == tint.Applied)
                continue;

            var baseColor = sprite.Color;
            var applied = Blend(baseColor, dirt);
            _sprite.SetColor((uid, sprite), applied);
            _tints[uid] = new(baseColor, applied, dirt);
        }
    }

    private void OnStartup(Entity<ClothingDirtableComponent> ent, ref ComponentStartup args)
    {
        if (TryComp(ent.Owner, out SpriteComponent? sprite))
            UpdateSprite(ent, sprite);
    }

    private void OnRemove(Entity<ClothingDirtableComponent> ent, ref ComponentRemove args)
    {
        _pending.Remove(ent.Owner);
        if (!_tints.Remove(ent.Owner, out var tint) ||
            !TryComp(ent.Owner, out SpriteComponent? sprite) ||
            sprite.Color != tint.Applied)
            return;

        _sprite.SetColor((ent.Owner, sprite), tint.Base);
    }

    private void OnState(Entity<ClothingDirtableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _pending.Add(ent.Owner);
        _item.VisualsChanged(ent.Owner);
    }

    private void OnEquipment(Entity<ClothingDirtableComponent> ent, ref GetEquipmentVisualsEvent args)
        => TintLayers(ent.Comp, args.Layers);

    private void OnInhand(Entity<ClothingDirtableComponent> ent, ref GetInhandVisualsEvent args)
        => TintLayers(ent.Comp, args.Layers);

    private static void TintLayers(ClothingDirtableComponent component, List<(string, PrototypeLayerData)> layers)
    {
        if (component.DirtColor is not { } dirt)
            return;
        for (var i = 0; i < layers.Count; i++)
        {
            var (key, source) = layers[i];
            var copy = Copy(source);
            copy.Color = Blend(source.Color ?? Color.White, dirt);
            layers[i] = (key, copy);
        }
    }

    private void UpdateSprite(Entity<ClothingDirtableComponent> ent, SpriteComponent sprite)
    {
        if (ent.Comp.DirtColor is not { } dirt)
        {
            if (_tints.Remove(ent.Owner, out var tint) && sprite.Color == tint.Applied)
                _sprite.SetColor((ent.Owner, sprite), tint.Base);
            return;
        }

        var baseColor = sprite.Color;
        if (_tints.TryGetValue(ent.Owner, out var previous))
        {
            if (previous.Dirt == dirt && previous.Applied == sprite.Color)
                return;
            if (previous.Applied == sprite.Color)
                baseColor = previous.Base;
        }

        var applied = Blend(baseColor, dirt);
        _sprite.SetColor((ent.Owner, sprite), applied);
        _tints[ent.Owner] = new(baseColor, applied, dirt);
    }

    private static Color Blend(Color baseColor, Color dirt)
        => new(
            baseColor.R * (1f - dirt.A) + dirt.R * dirt.A,
            baseColor.G * (1f - dirt.A) + dirt.G * dirt.A,
            baseColor.B * (1f - dirt.A) + dirt.B * dirt.A,
            baseColor.A);

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
            MapKeys = source.MapKeys == null ? null : new HashSet<string>(source.MapKeys),
            RenderingStrategy = source.RenderingStrategy,
            CopyToShaderParameters = source.CopyToShaderParameters,
            Cycle = source.Cycle,
            Loop = source.Loop,
        };

    private readonly record struct TintState(Color Base, Color Applied, Color Dirt);
}
