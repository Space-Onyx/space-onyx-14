using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.ItemSwitch;

public sealed partial class ItemSwitchSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSwitchComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ItemSwitchComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ItemSwitchComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ItemSwitchComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerb);
    }

    private void OnInit(Entity<ItemSwitchComponent> ent, ref ComponentInit args)
    {
        Switch(ent, ent.Comp.State, predicted: ent.Comp.Predictable);
    }

    private void OnUseInHand(Entity<ItemSwitchComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !ent.Comp.OnUse || ent.Comp.States.Count == 0)
            return;

        args.Handled = true;
        Switch(ent, Next(ent.Comp), args.User, ent.Comp.Predictable);
    }

    private void OnActivate(Entity<ItemSwitchComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !ent.Comp.OnActivate || ent.Comp.States.Count == 0)
            return;

        args.Handled = true;
        Switch(ent, Next(ent.Comp), args.User, ent.Comp.Predictable);
    }

    private void OnGetVerb(Entity<ItemSwitchComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.OnActivate || ent.Comp.States.Count == 0)
            return;

        var user = args.User;
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("item-switch-cycle"),
            Act = () => Switch(ent, Next(ent.Comp), user, ent.Comp.Predictable),
        });
    }

    private static string Next(ItemSwitchComponent comp)
    {
        var keys = comp.States.Keys.ToList();
        var index = keys.IndexOf(comp.State);
        return keys[(index + 1) % keys.Count];
    }

    public bool Switch(Entity<ItemSwitchComponent> ent, string key, EntityUid? user = null, bool predicted = true)
    {
        if (!ent.Comp.States.TryGetValue(key, out var state))
            return false;

        if (!ent.Comp.Predictable && _net.IsClient)
            return true;

        var attempt = new ItemSwitchAttemptEvent(user, key);
        RaiseLocalEvent(ent.Owner, ref attempt);
        if (attempt.Cancelled)
            return false;

        var nextAttack = TryComp(ent, out MeleeWeaponComponent? melee) ? melee.NextAttack : TimeSpan.Zero;

        if (ent.Comp.States.TryGetValue(ent.Comp.State, out var previous)
            && previous.RemoveComponents
            && previous.Components != null)
        {
            EntityManager.RemoveComponents(ent, previous.Components);
        }

        if (state.Components != null)
            EntityManager.AddComponents(ent, state.Components);

        if (nextAttack != TimeSpan.Zero && TryComp(ent, out melee))
            melee.NextAttack = nextAttack;

        ent.Comp.State = key;
        _item.SetHeldPrefix(ent, key);
        Dirty(ent);

        predicted &= ent.Comp.Predictable;
        if (predicted)
            _audio.PlayPredicted(state.Sound, ent, user);
        else
            _audio.PlayPvs(state.Sound, ent);

        var switched = new ItemSwitchedEvent(user, key, predicted);
        RaiseLocalEvent(ent.Owner, ref switched);
        return true;
    }
}
