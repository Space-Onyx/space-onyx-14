using Content.Server._Onyx.Chemistry.EntitySystems;
using Content.Shared._Onyx.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;

namespace Content.Server._Onyx.Chemistry.Components;

[RegisterComponent, Access(typeof(EnergyReagentDispenserSystem))]
public sealed partial class EnergyReagentDispenserComponent : Component
{
    [DataField] public ItemSlot BeakerSlot = new();
    [DataField] public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
    [DataField] public SoundSpecifier PowerSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
    [DataField] public Dictionary<string, float> Reagents = new();
    public EnergyReagentDispenserDispenseAmount Amount = EnergyReagentDispenserDispenseAmount.U10;
}
