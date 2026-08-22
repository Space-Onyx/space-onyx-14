using System.Linq;
using Content.Server._Onyx.Chemistry.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Onyx.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Chemistry.EntitySystems;

public sealed partial class EnergyReagentDispenserSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private BatterySystem _battery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EnergyReagentDispenserComponent, ComponentStartup>(Update);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, BoundUIOpenedEvent>(Update);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, SolutionChangedEvent>(Update);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, EntInsertedIntoContainerMessage>(Update);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, EntRemovedFromContainerMessage>(Update);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, PowerChangedEvent>(Update);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, MapInitEvent>(OnMapInit, before: [typeof(ItemSlotsSystem)]);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, EnergyReagentDispenserSetDispenseAmountMessage>(OnAmount);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, EnergyReagentDispenserDispenseReagentMessage>(OnDispense);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, EnergyReagentDispenserClearContainerSolutionMessage>(OnClear);
    }

    private void Update<T>(Entity<EnergyReagentDispenserComponent> ent, ref T _) => Update(ent);
    private void Update(Entity<EnergyReagentDispenserComponent> ent)
    {
        var item = _slots.GetItemOrNull(ent.Owner, SharedEnergyReagentDispenser.OutputSlotName);
        ContainerInfo? info = null;
        if (item is { Valid: true } && _solutions.TryGetFitsInDispenser(item.Value, out _, out var solution))
            info = new ContainerInfo(Name(item.Value), solution.Volume, solution.MaxVolume) { Reagents = solution.Contents };
        var inventory = ent.Comp.Reagents.Select(x => _prototypes.TryIndex<ReagentPrototype>(x.Key, out var p)
            ? new EnergyReagentInventoryItem(x.Key, p.LocalizedName, x.Value, p.SubstanceColor) : null).OfType<EnergyReagentInventoryItem>().ToList();
        inventory.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.Ordinal));
        TryComp(ent, out BatteryComponent? battery);
        TryComp(ent, out ApcPowerReceiverBatteryComponent? apcBattery);
        TryComp(ent, out ApcPowerReceiverComponent? apc);
        var charge = battery == null ? 0 : _battery.GetCharge((ent.Owner, battery));
        _ui.SetUiState(ent.Owner, EnergyReagentDispenserUiKey.Key, new EnergyReagentDispenserBoundUserInterfaceState(info, GetNetEntity(item), inventory, ent.Comp.Amount,
            charge, battery?.MaxCharge ?? 0, apcBattery?.BatteryRechargeRate ?? 0, apcBattery?.IdleLoad ?? 0, apcBattery?.Enabled ?? false, apc?.Powered ?? false));
    }

    private void OnAmount(Entity<EnergyReagentDispenserComponent> ent, ref EnergyReagentDispenserSetDispenseAmountMessage msg) { ent.Comp.Amount = msg.Amount; Update(ent); Click(ent); }
    private void OnDispense(Entity<EnergyReagentDispenserComponent> ent, ref EnergyReagentDispenserDispenseReagentMessage msg)
    {
        var item = _slots.GetItemOrNull(ent.Owner, SharedEnergyReagentDispenser.OutputSlotName);
        if (item is not { Valid: true } || !_solutions.TryGetFitsInDispenser(item.Value, out var solution, out _)
            || !TryComp(ent, out BatteryComponent? battery) || !ent.Comp.Reagents.TryGetValue(msg.ReagentId, out var cost)) return;
        var power = cost * (int) ent.Comp.Amount;
        var charge = _battery.GetCharge((ent.Owner, battery));
        if (charge < power) { _audio.PlayPvs(ent.Comp.PowerSound, ent, AudioParams.Default.WithVolume(-2f)); return; }
        if (!_solutions.TryAddSolution(solution.Value, new Solution(msg.ReagentId, (int) ent.Comp.Amount))) return;
        _battery.SetCharge((ent.Owner, battery), charge - power); Click(ent); Update(ent);
    }

    private void OnClear(Entity<EnergyReagentDispenserComponent> ent, ref EnergyReagentDispenserClearContainerSolutionMessage msg)
    {
        var item = _slots.GetItemOrNull(ent.Owner, SharedEnergyReagentDispenser.OutputSlotName);
        if (item is not { Valid: true } || !_solutions.TryGetFitsInDispenser(item.Value, out var solution, out var contents)) return;
        var refund = contents.Sum(reagent => ent.Comp.Reagents.TryGetValue(reagent.Reagent.Prototype, out var cost)
            ? cost * (int) reagent.Quantity
            : 0);
        if (refund > 0 && TryComp(ent, out BatteryComponent? battery))
            _battery.ChangeCharge((ent.Owner, battery), refund);
        _solutions.RemoveAllSolution(solution.Value); Click(ent); Update(ent);
    }
    private void Click(Entity<EnergyReagentDispenserComponent> ent) => _audio.PlayPvs(ent.Comp.ClickSound, ent, AudioParams.Default.WithVolume(-2f));
    private void OnMapInit(Entity<EnergyReagentDispenserComponent> ent, ref MapInitEvent _) => _slots.AddItemSlot(ent.Owner, SharedEnergyReagentDispenser.OutputSlotName, ent.Comp.BeakerSlot);
}
