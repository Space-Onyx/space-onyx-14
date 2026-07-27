using Content.Server.Popups;
using Content.Shared._Onyx.Clothing;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server._Onyx.Clothing;

public sealed partial class ShowerSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private ClothingDirtSystem _dirt = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private PopupSystem _popup = default!;

    private readonly HashSet<EntityUid> _entities = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShowerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ShowerComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<ShowerComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ShowerComponent>();
        while (query.MoveNext(out var uid, out var shower))
        {
            if (!shower.Enabled || (shower.WashAccumulator += frameTime) < shower.WashInterval)
                continue;
            shower.WashAccumulator %= shower.WashInterval;
            WashArea(uid, shower);
        }
    }

    private void OnStartup(Entity<ShowerComponent> ent, ref ComponentStartup args)
        => _appearance.SetData(ent.Owner, ShowerVisuals.Enabled, ent.Comp.Enabled);

    private void OnInteract(Entity<ShowerComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        ent.Comp.Enabled = !ent.Comp.Enabled;
        ent.Comp.WashAccumulator = 0;
        Dirty(ent);
        _appearance.SetData(ent.Owner, ShowerVisuals.Enabled, ent.Comp.Enabled);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Enabled ? "shower-component-switched-on" : "shower-component-switched-off"),
            ent.Owner, args.User, PopupType.Small);
        args.Handled = true;
    }

    private void OnExamined(Entity<ShowerComponent> ent, ref ExaminedEvent args)
        => args.PushMarkup(Loc.GetString(ent.Comp.Enabled ? "shower-component-examine-on" : "shower-component-examine-off"));

    private void WashArea(EntityUid uid, ShowerComponent shower)
    {
        _entities.Clear();
        _lookup.GetEntitiesInRange(Transform(uid).Coordinates, shower.WashRange, _entities, LookupFlags.Dynamic);
        foreach (var wearer in _entities)
        {
            if (!_inventory.TryGetContainerSlotEnumerator(wearer, out var enumerator, shower.TargetSlots))
                continue;
            while (enumerator.NextItem(out var item))
                _dirt.TryAddCleanerToClothing(item, new ReagentId(shower.CleanerReagent, null), shower.WashAmount);
        }
    }
}
