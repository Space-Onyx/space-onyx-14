using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Body;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Overlays;

public sealed partial class SharedThermalVisionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly SoundSpecifier ActivateSound =
        new SoundPathSpecifier("/Audio/_Onyx/Items/Goggles/activate.ogg");

    private static readonly SoundSpecifier DeactivateSound =
        new SoundPathSpecifier("/Audio/_Onyx/Items/Goggles/deactivate.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThermalVisionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ThermalVisionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ThermalVisionComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<ThermalVisionComponent, ToggleThermalVisionEvent>(OnToggle);
    }

    private void OnInit(Entity<ThermalVisionComponent> ent, ref ComponentInit args)
    {
        _actions.AddAction(ent, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
    }

    private void OnShutdown(Entity<ThermalVisionComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ToggleActionEntity);
        RefreshWearer(ent);
    }

    private void OnGetItemActions(Entity<ThermalVisionComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is SlotFlags.POCKET or null)
            return;

        args.AddAction(ent.Comp.ToggleActionEntity);
    }

    private void OnToggle(Entity<ThermalVisionComponent> ent, ref ToggleThermalVisionEvent args)
    {
        if (ent.Comp.PulseTime > 0f)
        {
            ent.Comp.Enabled = true;
            ent.Comp.PulseRemaining = ent.Comp.PulseTime;
        }
        else
        {
            ent.Comp.Enabled = !ent.Comp.Enabled;
        }

        _actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Enabled);
        _audio.PlayPredicted(ent.Comp.Enabled ? ActivateSound : DeactivateSound, ent, args.Performer);
        Dirty(ent);
        RefreshWearer(ent);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ThermalVisionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled || comp.PulseTime <= 0f)
                continue;

            comp.PulseRemaining -= frameTime;
            if (comp.PulseRemaining > 0f)
                continue;

            comp.Enabled = false;
            comp.PulseRemaining = 0f;
            _actions.SetToggled(comp.ToggleActionEntity, false);
            if (_net.IsServer)
                Dirty(uid, comp);
            RefreshWearer((uid, comp));
        }
    }

    private void RefreshWearer(Entity<ThermalVisionComponent> ent)
    {
        var wearer = HasComp<BodyComponent>(ent) ? ent.Owner : Transform(ent).ParentUid;
        var ev = new RefreshEquipmentHudEvent<ThermalVisionComponent>(~SlotFlags.POCKET);
        RaiseLocalEvent(wearer, ref ev);
    }
}
