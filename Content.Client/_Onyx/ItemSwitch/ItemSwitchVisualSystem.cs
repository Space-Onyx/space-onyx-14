using Content.Client.Items;
using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._Onyx.ItemSwitch;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;

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
        Subs.ItemStatus<ItemSwitchComponent>(ent => new ItemSwitchStatusControl(ent));
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

public sealed class ItemSwitchStatusControl : PollingItemStatusControl<ItemSwitchStatusControl.Data>
{
    private readonly Entity<ItemSwitchComponent> _item;
    private readonly RichTextLabel _label;

    public ItemSwitchStatusControl(Entity<ItemSwitchComponent> item)
    {
        _item = item;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };

        if (item.Comp.ShowLabel)
            AddChild(_label);
    }

    protected override Data PollData()
    {
        return new Data(_item.Comp.State);
    }

    protected override void Update(in Data data)
    {
        _label.SetMarkup(Loc.GetString("itemswitch-component-on-examine-detailed-message",
            ("state", Loc.GetString($"itemswitch-component-state-{data.State}"))));
    }

    public readonly record struct Data(string State);
}
