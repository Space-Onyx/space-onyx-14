using Content.Shared._Onyx.ItemSwitch;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.ItemSwitch;

public sealed partial class ItemSwitchVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSwitchComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ItemSwitchComponent, AfterAutoHandleStateEvent>(OnStateChanged);
        SubscribeLocalEvent<ItemSwitchComponent, ItemSwitchedEvent>(OnSwitched);
    }

    private void OnStartup(Entity<ItemSwitchComponent> ent, ref ComponentStartup args)
    {
        UpdateSprite(ent);
    }

    private void OnStateChanged(Entity<ItemSwitchComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite(ent);
    }

    private void OnSwitched(Entity<ItemSwitchComponent> ent, ref ItemSwitchedEvent args)
    {
        UpdateSprite(ent);
    }

    private void UpdateSprite(Entity<ItemSwitchComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite)
            || !ent.Comp.States.TryGetValue(ent.Comp.State, out var state)
            || state.Sprite == null)
        {
            return;
        }

        _sprite.LayerSetSprite((ent.Owner, sprite), 0, state.Sprite);
    }
}
