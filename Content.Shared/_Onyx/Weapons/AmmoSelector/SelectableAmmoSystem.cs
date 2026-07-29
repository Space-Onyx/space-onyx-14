using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Weapons.AmmoSelector;

public sealed partial class SelectableAmmoSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedBatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AmmoSelectorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AmmoSelectorComponent, AmmoSelectedMessage>(OnSelected);
        SubscribeLocalEvent<AmmoSelectorComponent, ExaminedEvent>(OnExamine);
    }

    private void OnMapInit(Entity<AmmoSelectorComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.DefaultPrototype is { } defaultPrototype)
            TrySetPrototype(ent, defaultPrototype);
        else if (ent.Comp.Prototypes.Count > 0)
            TrySetPrototype(ent, ent.Comp.Prototypes.First());
    }

    private void OnSelected(Entity<AmmoSelectorComponent> ent, ref AmmoSelectedMessage args)
    {
        if (!ent.Comp.Prototypes.Contains(args.ProtoId) || !TrySetPrototype(ent, args.ProtoId))
            return;

        var selected = _prototype.Index(args.ProtoId);
        _popup.PopupEntity(Loc.GetString("mode-selected", ("mode", Loc.GetString("ent-" + selected.ProtoId))), ent, args.Actor);
        _audio.PlayPredicted(ent.Comp.SoundSelect, ent, args.Actor);
    }

    private void OnExamine(Entity<AmmoSelectorComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.CurrentlySelected is not { } selected || !_prototype.TryIndex(selected, out var prototype))
            return;

        args.PushMarkup(Loc.GetString("ammo-selector-examine-mode", ("mode", Loc.GetString("ent-" + prototype.ProtoId))));
    }

    public bool TrySetPrototype(Entity<AmmoSelectorComponent> ent, ProtoId<SelectableAmmoPrototype> id)
    {
        if (!_prototype.TryIndex(id, out var selected) || !TryComp(ent, out BatteryAmmoProviderComponent? provider))
            return false;

        provider.Prototype = selected.ProtoId;
        provider.FireCost = selected.FireCost;
        if (TryComp(ent, out Content.Shared.Power.Components.BatteryComponent? battery))
        {
            provider.Shots = (int) (_battery.GetCharge((ent.Owner, battery)) / provider.FireCost);
            provider.Capacity = (int) (battery.MaxCharge / provider.FireCost);
        }
        Dirty(ent.Owner, provider);
        ent.Comp.CurrentlySelected = id;

        if (selected.Color != null && TryComp(ent, out AppearanceComponent? appearance))
            _appearance.SetData(ent, ToggleableVisuals.Color, selected.Color, appearance);

        Dirty(ent);
        return true;
    }
}
