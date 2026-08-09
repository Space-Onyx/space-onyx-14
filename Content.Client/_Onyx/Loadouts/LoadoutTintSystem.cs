using Content.Client.Clothing;
using Content.Shared._Onyx.Loadouts;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Onyx.Loadouts;

public sealed partial class LoadoutTintSystem : EntitySystem
{
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LoadoutTintComponent, ComponentInit>(OnTintChanged);
        SubscribeLocalEvent<LoadoutTintComponent, AfterAutoHandleStateEvent>(OnTintChanged);
        SubscribeLocalEvent<LoadoutTintComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals, after: [typeof(ClientClothingSystem)]);
    }

    private void OnTintChanged<T>(Entity<LoadoutTintComponent> ent, ref T args)
    {
        ApplyItemTint(ent);
        _item.VisualsChanged(ent);
    }

    private static void OnGetEquipmentVisuals(Entity<LoadoutTintComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        foreach (var (_, layer) in args.Layers)
            layer.Color = ent.Comp.Color;
    }

    public void SetTint(EntityUid uid, Color color)
    {
        var tint = EnsureComp<LoadoutTintComponent>(uid);
        tint.Color = color;
        Dirty(uid, tint);
        ApplyItemTint((uid, tint));
        _item.VisualsChanged(uid);
    }

    private void ApplyItemTint(Entity<LoadoutTintComponent> ent)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            _sprite.SetColor((ent, sprite), ent.Comp.Color);
    }
}
