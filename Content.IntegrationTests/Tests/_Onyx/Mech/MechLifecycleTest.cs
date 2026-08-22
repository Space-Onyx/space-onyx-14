using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Onyx.Sprinting;
using Content.Server.Mech.Equipment.Components;
using Content.Server.Mech.Systems;
using Content.Shared.Actions;
using Content.Shared._Onyx.Carrying;
using Content.Shared._Onyx.Sprinting;
using Content.Shared._Onyx.TileMovement;
using Content.Shared.CCVar;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Zombies;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Onyx.Mech;

[TestOf(typeof(MechSystem))]
public sealed class MechLifecycleTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: TestMech
  parent: BaseMech
  components:
  - type: Mech
    entryDelay: 0
    exitDelay: 0

- type: entity
  id: TestMechPilot
  components:
  - type: InputMover
  - type: Hands
    hands:
      hand_right:
        location: Right
      hand_left:
        location: Left
    sortedHands:
    - hand_right
    - hand_left
  - type: NpcFactionMember
    factions:
    - Syndicate
  - type: Sprinter
    timeBetweenSprints: 0
  - type: Stamina

- type: entity
  id: TestNeutralMechPilot
  components:
  - type: InputMover
  - type: Hands
    hands:
      hand_right:
        location: Right
      hand_left:
        location: Left
    sortedHands: [ hand_right, hand_left ]

- type: entity
  id: TestHandlessMechPilot
  components:
  - type: InputMover

- type: entity
  id: TestHamsterMechPilot
  parent: TestHandlessMechPilot
  components:
  - type: Tag
    tags: [ Hamster ]

- type: entity
  id: TestVimMechPilot
  parent: TestHandlessMechPilot
  components:
  - type: Tag
    tags: [ VimPilot ]

- type: entity
  id: TestFactionMech
  parent: TestMech
  components:
  - type: NpcFactionMember
    factions: [ NanoTrasen ]

- type: entity
  id: TestWhitelistMech
  parent: TestMech
  components:
  - type: Mech
    pilotWhitelist:
      components:
      - Hands

- type: entity
  id: TestMechHeldItem
  components:
  - type: Item

- type: entity
  id: TestMechUndroppableItem
  components:
  - type: Item
  - type: Unremoveable

- type: entity
  id: TestCarriedEntity
  components:
  - type: Carriable

- type: entity
  id: TestMechBattery
  components:
  - type: Battery
    maxCharge: 100
    startingCharge: 100

- type: entity
  id: TestMechPartialBattery
  components:
  - type: Battery
    maxCharge: 100
    startingCharge: 37

- type: entity
  id: TestMechWithPartialBattery
  parent: TestMech
  components:
  - type: ContainerFill
    containers:
      mech-battery-slot:
      - TestMechPartialBattery

- type: entity
  id: TestRestrictedMech
  parent: TestMech
  components:
  - type: Mech
    equipmentWhitelist:
      components:
      - Battery

- type: entity
  id: TestRestrictedEquipment
  components:
  - type: MechEquipment

- type: entity
  id: TestMechGun
  components:
  - type: MechEquipment
  - type: Gun
  - type: BatteryAmmoProvider
    proto: BulletPistol
    fireCost: 10
  - type: Battery
    maxCharge: 10
    startingCharge: 0

- type: entity
  id: TestMechHitscanGun
  components:
  - type: MechEquipment
  - type: Gun
  - type: BatteryAmmoProvider
    proto: RedLaser
    fireCost: 10
  - type: Battery
    maxCharge: 10
    startingCharge: 0

- type: entity
  id: TestCombatMech
  parent: [ TestMech, CombatMech ]

- type: entity
  id: TestCombatMechEquipment
  parent: [ BaseMechEquipment, CombatMechEquipment ]

- type: entity
  id: TestDebugMechEquipment
  parent: [ BaseMechEquipment, DebugMechEquipment ]

- type: entity
  id: TestMechGrabber
  parent: BaseMechEquipment
  components:
  - type: MechGrabber
    grabDelay: 0
    grabEnergyDelta: -30
    maxContents: 2
    blacklist:
      tags: [ MechUnGrabbable ]
      components: [ Mech ]
  - type: ContainerContainer

- type: entity
  id: TestMechGrabTarget

- type: entity
  id: TestMechGrabBlockedTarget
  components:
  - type: Tag
    tags: [ MechUnGrabbable ]
""";

    [SidedDependency(Side.Server)] private readonly MechSystem _mech = null!;
    [SidedDependency(Side.Server)] private readonly VehicleSystem _vehicle = null!;
    [SidedDependency(Side.Server)] private readonly SharedContainerSystem _container = null!;
    [SidedDependency(Side.Server)] private readonly SharedBatterySystem _battery = null!;
    [SidedDependency(Side.Server)] private readonly SharedGunSystem _gun = null!;
    [SidedDependency(Side.Server)] private readonly SharedHandsSystem _hands = null!;
    [SidedDependency(Side.Server)] private readonly SprintingSystem _sprinting = null!;
    [SidedDependency(Side.Server)] private readonly SharedActionsSystem _actions = null!;
    [SidedDependency(Side.Server)] private readonly CarryingSystem _carrying = null!;

    [Test]
    [RunOnSide(Side.Server)]
    public async Task ExplicitEjectionCleansPilotState()
    {
        var mech = SSpawn("TestMech");
        var pilot = SSpawn("TestMechPilot");
        var component = SComp<MechComponent>(mech);

        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        AssertPilotInserted(mech, pilot, component);

        Assert.That(_vehicle.TryExit(mech), Is.True);
        AssertPilotRemoved(mech, pilot, component);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task OutOfBandRemovalCleansPilotState()
    {
        var mech = SSpawn("TestMech");
        var pilot = SSpawn("TestMechPilot");
        var component = SComp<MechComponent>(mech);

        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        AssertPilotInserted(mech, pilot, component);

        Assert.That(_vehicle.TryExit(mech), Is.True);
        AssertPilotRemoved(mech, pilot, component);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task OutOfBandEquipmentRemovalClearsOwnerAndSelection()
    {
        var mech = SSpawn("TestMech");
        var battery = SSpawn("TestMechBattery");
        var gun = SSpawn("TestMechGun");
        var component = SComp<MechComponent>(mech);
        var equipment = SComp<MechEquipmentComponent>(gun);
        var batteryComp = SComp<BatteryComponent>(battery);

        _mech.InsertBattery(mech, battery, component, batteryComp);
        _mech.InsertEquipment(mech, gun, component, equipment);
        _mech.CycleEquipment(mech, component);
        Assert.That(component.CurrentSelectedEquipment, Is.EqualTo(gun));

        Assert.That(_container.Remove(gun, component.EquipmentContainer, force: true), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(equipment.EquipmentOwner, Is.Null);
            Assert.That(component.CurrentSelectedEquipment, Is.Null);
            Assert.That(component.EquipmentContainer.Contains(gun), Is.False);
        });

        var charge = _battery.GetCharge((battery, batteryComp));
        Assert.That(TakeAmmo(gun, 1), Is.Empty);
        Assert.That(_battery.GetCharge((battery, batteryComp)), Is.EqualTo(charge));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task PilotPoliciesRestoreHandsAndFactions()
    {
        var mech = SSpawn("TestMech");
        var pilot = SSpawn("TestMechPilot");
        var component = SComp<MechComponent>(mech);

        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        Assert.That(_hands.EnumerateHeld(pilot)
            .Count(held => SEntMan.HasComponent<VirtualItemComponent>(held)), Is.EqualTo(2));
        Assert.That(SComp<NpcFactionMemberComponent>(mech).Factions, Is.EquivalentTo(new[] { "Syndicate" }));

        Assert.That(_vehicle.TryExit(mech), Is.True);
        Assert.That(_hands.EnumerateHeld(pilot).Any(), Is.False);
        Assert.That(SEntMan.HasComponent<NpcFactionMemberComponent>(mech), Is.False);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task HeldItemsDropAndUndroppableItemsRejectAtomically()
    {
        var mech = SSpawn("TestMech");
        var pilot = SSpawn("TestMechPilot");
        var left = SSpawn("TestMechHeldItem");
        var right = SSpawn("TestMechHeldItem");
        var hands = SComp<HandsComponent>(pilot);
        var handIds = _hands.EnumerateHands((pilot, hands)).Take(2).ToArray();

        Assert.That(_hands.TryPickup(pilot, left, handIds[0], handsComp: hands), Is.True);
        Assert.That(_hands.TryPickup(pilot, right, handIds[1], handsComp: hands), Is.True);
        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        Assert.That(_hands.EnumerateHeld(pilot)
            .Count(item => SEntMan.HasComponent<VirtualItemComponent>(item)), Is.EqualTo(2));
        Assert.That(SEntMan.Deleted(left), Is.False);
        Assert.That(SEntMan.Deleted(right), Is.False);
        Assert.That(_vehicle.TryExit(mech), Is.True);

        var blocked = SSpawn("TestMechUndroppableItem");
        Assert.That(_hands.TryPickup(pilot, blocked, handIds[0], handsComp: hands), Is.True);
        Assert.That(_vehicle.TryEnter(mech, pilot), Is.False);
        Assert.That(_hands.IsHolding((pilot, hands), blocked), Is.True);
        Assert.That(SComp<VehicleComponent>(mech).Operator, Is.Null);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task DirectInsertionEnforcesWhitelist()
    {
        var mech = SSpawn("TestWhitelistMech");
        var handless = SSpawn("TestHandlessMechPilot");
        var allowed = SSpawn("TestNeutralMechPilot");

        Assert.That(_vehicle.TryEnter(mech, handless), Is.False);
        Assert.That(_vehicle.TryEnter(mech, allowed), Is.True);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task CarryingEndsBeforeHandsAreReserved()
    {
        var mech = SSpawn("TestMech");
        var pilot = SSpawn("TestMechPilot");
        var carried = SSpawn("TestCarriedEntity");

        _carrying.Carry(pilot, carried);
        Assert.That(SEntMan.HasComponent<CarryingComponent>(pilot), Is.True);
        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        Assert.That(SEntMan.HasComponent<CarryingComponent>(pilot), Is.False);
        Assert.That(SEntMan.HasComponent<BeingCarriedComponent>(carried), Is.False);
        Assert.That(_hands.EnumerateHeld(pilot)
            .Count(item => SEntMan.HasComponent<VirtualItemComponent>(item)), Is.EqualTo(2));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task NeutralPilotTemporarilyClearsAndRestoresMechFactions()
    {
        var mech = SSpawn("TestFactionMech");
        var pilot = SSpawn("TestNeutralMechPilot");
        var component = SComp<MechComponent>(mech);

        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        Assert.That(SComp<NpcFactionMemberComponent>(mech).Factions, Is.Empty);
        Assert.That(_vehicle.TryExit(mech), Is.True);
        Assert.That(SComp<NpcFactionMemberComponent>(mech).Factions, Is.EquivalentTo(new[] { "NanoTrasen" }));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task SprintCannotStartWithoutPolicyDependencies()
    {
        var mech = SSpawn("TestMech");
        var sprint = SComp<SprinterComponent>(mech);

        sprint.CanSprint = false;
        _sprinting.ToggleSprint(mech, sprint, true);
        Assert.That(sprint.IsSprinting, Is.False);

        sprint.CanSprint = true;
        SEntMan.RemoveComponent<Content.Shared.Damage.Components.StaminaComponent>(mech);
        _sprinting.ToggleSprint(mech, sprint, true);
        Assert.That(sprint.IsSprinting, Is.False);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task ExistingMechWhitelistAndZombiePoliciesApplyToDirectInsertion()
    {
        var hamtr = SSpawn("MechHamtr");
        var hamster = SSpawn("TestHamsterMechPilot");
        var vim = SSpawn("MechVim");
        var vimPilot = SSpawn("TestVimMechPilot");
        var human = SSpawn("TestMechPilot");

        Assert.That(_vehicle.TryEnter(hamtr, hamster), Is.True);
        Assert.That(_vehicle.TryExit(hamtr), Is.True);
        Assert.That(_vehicle.TryEnter(hamtr, human), Is.False);
        Assert.That(_vehicle.TryEnter(vim, vimPilot), Is.True);
        Assert.That(_vehicle.TryExit(vim), Is.True);

        SEntMan.AddComponent<ZombieComponent>(human);
        Assert.That(_vehicle.TryEnter(SSpawn("TestMech"), human), Is.False);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task TileMovementRelayFollowsPilotLifecycle()
    {
        var mech = SSpawn("TestMech");
        var pilot = SSpawn("TestMechPilot");
        SEntMan.AddComponent<TileMovementComponent>(pilot);

        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        Assert.That(SEntMan.HasComponent<TileMovementComponent>(mech), Is.True);
        Assert.That(SEntMan.HasComponent<TileMovementRelayComponent>(mech), Is.True);
        Assert.That(_vehicle.TryExit(mech), Is.True);
        Assert.That(SEntMan.HasComponent<TileMovementRelayComponent>(mech), Is.False);
        Assert.That(SEntMan.HasComponent<TileMovementComponent>(mech), Is.False);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task SprintStateFollowsMechLifecycle()
    {
        var mech = SSpawn("TestMech");
        var pilot = SSpawn("TestMechPilot");
        var component = SComp<MechComponent>(mech);
        var pilotSprint = SComp<SprinterComponent>(pilot);
        var mechSprint = SComp<SprinterComponent>(mech);

        pilotSprint.IsSprinting = true;
        mechSprint.IsSprinting = true;
        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(pilotSprint.IsSprinting, Is.False);
            Assert.That(mechSprint.IsSprinting, Is.False);
        });

        _sprinting.ToggleSprint(mech, mechSprint, true);
        Assert.That(mechSprint.IsSprinting, Is.True);
        Assert.That(pilotSprint.IsSprinting, Is.False);

        Assert.That(_vehicle.TryExit(mech), Is.True);
        Assert.That(mechSprint.IsSprinting, Is.False);
        Assert.That(pilotSprint.IsSprinting, Is.False);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task MapInitUsesAuthoredBatteryCharge()
    {
        var mech = SSpawn("TestMechWithPartialBattery");
        var component = SComp<MechComponent>(mech);
        var battery = component.BatterySlot.ContainedEntity;

        Assert.That(battery, Is.Not.Null);
        var batteryComp = SComp<BatteryComponent>(battery!.Value);
        Assert.Multiple(() =>
        {
            Assert.That(_battery.GetCharge((battery.Value, batteryComp)), Is.EqualTo(37));
            Assert.That(component.Energy, Is.EqualTo(FixedPoint2.New(37)));
            Assert.That(component.MaxEnergy, Is.EqualTo(FixedPoint2.New(100)));
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task EmpTracksRepeatedZeroAndSwappedBattery()
    {
        var mech = SSpawn("TestMech");
        var first = SSpawn("TestMechBattery");
        var second = SSpawn("TestMechBattery");
        var component = SComp<MechComponent>(mech);
        var firstComp = SComp<BatteryComponent>(first);
        var secondComp = SComp<BatteryComponent>(second);

        _mech.InsertBattery(mech, first, component, firstComp);
        RaiseEmp(mech, 20);
        RaiseEmp(mech, 30);
        Assert.That(_battery.GetCharge((first, firstComp)), Is.EqualTo(50));
        Assert.That(component.Energy, Is.EqualTo(FixedPoint2.New(50)));

        var zero = RaiseEmp(mech, 0);
        Assert.That(zero.Affected, Is.False);
        Assert.That(zero.Disabled, Is.False);
        Assert.That(_container.Remove(first, component.BatterySlot, force: true), Is.True);
        Assert.That(component.Energy, Is.Zero);
        Assert.That(component.MaxEnergy, Is.Zero);

        _battery.SetCharge((second, secondComp), 35);
        _mech.InsertBattery(mech, second, component, secondComp);
        RaiseEmp(mech, 10);
        Assert.That(_battery.GetCharge((first, firstComp)), Is.EqualTo(50));
        Assert.That(_battery.GetCharge((second, secondComp)), Is.EqualTo(25));
        Assert.That(component.Energy, Is.EqualTo(FixedPoint2.New(25)));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task InteractionEmagClearsEquipmentWhitelist()
    {
        var mech = SSpawn("TestRestrictedMech");
        var equipment = SSpawn("TestRestrictedEquipment");
        var user = SSpawn("TestMechPilot");
        var component = SComp<MechComponent>(mech);
        var equipmentComp = SComp<MechEquipmentComponent>(equipment);

        _mech.InsertEquipment(mech, equipment, component, equipmentComp);
        Assert.That(equipmentComp.EquipmentOwner, Is.Null);

        var access = new GotEmaggedEvent(user, EmagType.Access);
        SEntMan.EventBus.RaiseLocalEvent(mech, ref access);
        Assert.That(component.EquipmentWhitelist, Is.Not.Null);

        var interaction = new GotEmaggedEvent(user, EmagType.Interaction);
        SEntMan.EventBus.RaiseLocalEvent(mech, ref interaction);
        Assert.That(component.EquipmentWhitelist, Is.Null);
        _mech.InsertEquipment(mech, equipment, component, equipmentComp);
        Assert.That(equipmentComp.EquipmentOwner, Is.EqualTo(mech));

        interaction = new GotEmaggedEvent(user, EmagType.Interaction);
        SEntMan.EventBus.RaiseLocalEvent(mech, ref interaction);
        Assert.That(interaction.Handled, Is.False);

        var protectedMech = SSpawn("TestRestrictedMech");
        var protectedComp = SComp<MechComponent>(protectedMech);
        protectedComp.BreakOnEmag = false;
        interaction = new GotEmaggedEvent(user, EmagType.Interaction);
        SEntMan.EventBus.RaiseLocalEvent(protectedMech, ref interaction);
        Assert.That(protectedComp.EquipmentWhitelist, Is.Not.Null);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task LightActionFollowsPilotLifecycle()
    {
        var mech = SSpawn("TestMech");
        var pilot = SSpawn("TestMechPilot");
        var component = SComp<MechComponent>(mech);
        var flashlight = SComp<UnpoweredFlashlightComponent>(mech);

        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        Assert.That(_actions.GetActions(pilot).Count(action => action.Owner == flashlight.ToggleActionEntity), Is.EqualTo(1));
        var flashlightSystem = SEntMan.System<UnpoweredFlashlightSystem>();
        flashlightSystem.SetLight((mech, flashlight), true, pilot, quiet: true);
        Assert.That(flashlight.LightOn, Is.True);
        Assert.That(_vehicle.TryExit(mech), Is.True);
        Assert.That(_actions.GetActions(pilot).Any(action => action.Owner == flashlight.ToggleActionEntity), Is.False);
        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        Assert.That(_actions.GetActions(pilot).Count(action => action.Owner == flashlight.ToggleActionEntity), Is.EqualTo(1));
        _mech.SetIntegrity(mech, 0, component);
        Assert.That(SComp<VehicleComponent>(mech).Operator, Is.Null);
        Assert.That(_actions.GetActions(pilot).Any(action => action.Owner == flashlight.ToggleActionEntity), Is.False);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task EmpAndGunUseMechBattery()
    {
        var mech = SSpawn("TestMech");
        var battery = SSpawn("TestMechBattery");
        var gun = SSpawn("TestMechGun");
        var component = SComp<MechComponent>(mech);
        var batteryComp = SComp<BatteryComponent>(battery);
        var provider = SComp<BatteryAmmoProviderComponent>(gun);
        var internalBattery = SComp<BatteryComponent>(gun);

        _mech.InsertBattery(mech, battery, component, batteryComp);
        _mech.InsertEquipment(mech, gun, component);

        var emp = new EmpPulseEvent(20, false, false, TimeSpan.Zero, null);
        SEntMan.EventBus.RaiseLocalEvent(mech, ref emp);
        Assert.That(_battery.GetCharge((battery, batteryComp)), Is.EqualTo(80));

        _battery.SetCharge((battery, batteryComp), 10);
        _gun.UpdateShots((gun, provider));
        Assert.That(provider.Shots, Is.EqualTo(1));

        var ammo = new List<(EntityUid? Entity, IShootable Shootable)>();
        var takeAmmo = new TakeAmmoEvent(1, ammo, SEntMan.GetComponent<TransformComponent>(gun).Coordinates, null);
        SEntMan.EventBus.RaiseLocalEvent(gun, takeAmmo);
        Assert.That(ammo, Has.Count.EqualTo(1));
        Assert.That(_battery.GetCharge((battery, batteryComp)), Is.Zero);
        Assert.That(component.Energy, Is.Zero);
        Assert.That(provider.Shots, Is.Zero);
        Assert.That(_battery.GetCharge((gun, internalBattery)), Is.Zero);

        ammo.Clear();
        SEntMan.EventBus.RaiseLocalEvent(gun, new TakeAmmoEvent(1, ammo,
            SEntMan.GetComponent<TransformComponent>(gun).Coordinates, null));
        Assert.That(ammo, Is.Empty);

        _battery.SetCharge((battery, batteryComp), 9);
        ammo.Clear();
        SEntMan.EventBus.RaiseLocalEvent(gun, new TakeAmmoEvent(1, ammo,
            SEntMan.GetComponent<TransformComponent>(gun).Coordinates, null));
        Assert.That(ammo, Is.Empty);
        Assert.That(_battery.GetCharge((battery, batteryComp)), Is.EqualTo(9));

        _mech.RemoveEquipment(mech, gun, component);
        Assert.That(SComp<MechEquipmentComponent>(gun).EquipmentOwner, Is.Null);
        Assert.That(provider.Shots, Is.Zero);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task MechBatteryLimitsBurstAndHitscanAmmo()
    {
        var mech = SSpawn("TestMech");
        var battery = SSpawn("TestMechBattery");
        var projectileGun = SSpawn("TestMechGun");
        var hitscanGun = SSpawn("TestMechHitscanGun");
        var component = SComp<MechComponent>(mech);
        var batteryComp = SComp<BatteryComponent>(battery);

        _mech.InsertBattery(mech, battery, component, batteryComp);
        _mech.InsertEquipment(mech, projectileGun, component);
        _battery.SetCharge((battery, batteryComp), 25);

        var projectileAmmo = TakeAmmo(projectileGun, 3);
        Assert.That(projectileAmmo, Has.Count.EqualTo(2));
        Assert.That(_battery.GetCharge((battery, batteryComp)), Is.EqualTo(5));

        _mech.RemoveEquipment(mech, projectileGun, component);
        _mech.InsertEquipment(mech, hitscanGun, component);
        _battery.SetCharge((battery, batteryComp), 10);

        var hitscanAmmo = TakeAmmo(hitscanGun, 1);
        Assert.That(hitscanAmmo, Has.Count.EqualTo(1));
        Assert.That(hitscanAmmo[0].Shootable, Is.InstanceOf<Content.Shared.Weapons.Hitscan.Components.HitscanAmmoComponent>());
        Assert.That(_battery.GetCharge((battery, batteryComp)), Is.Zero);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task OutsideMechCVarControlsRemovedGun()
    {
        var mech = SSpawn("TestMech");
        var gun = SSpawn("TestMechGun");
        var user = SSpawn("TestMechPilot");
        var component = SComp<MechComponent>(mech);
        var gunComp = SComp<GunComponent>(gun);

        _mech.InsertEquipment(mech, gun, component);
        _mech.RemoveEquipment(mech, gun, component);

        await OverrideCVar(Side.Server, CCVars.MechGunOutsideMech, false);
        var attempted = new ShotAttemptedEvent { User = user, Used = (gun, gunComp) };
        SEntMan.EventBus.RaiseLocalEvent(gun, ref attempted);
        Assert.That(attempted.Cancelled, Is.True);
        var internalBattery = SComp<BatteryComponent>(gun);
        _battery.SetCharge((gun, internalBattery), 10);
        Assert.That(TakeAmmo(gun, 1), Is.Empty);
        Assert.That(_battery.GetCharge((gun, internalBattery)), Is.EqualTo(10));

        await OverrideCVar(Side.Server, CCVars.MechGunOutsideMech, true);
        _battery.SetCharge((gun, internalBattery), 10);
        attempted = new ShotAttemptedEvent { User = user, Used = (gun, gunComp) };
        SEntMan.EventBus.RaiseLocalEvent(gun, ref attempted);
        Assert.That(attempted.Cancelled, Is.False);
        Assert.That(TakeAmmo(gun, 1), Has.Count.EqualTo(1));
        Assert.That(_battery.GetCharge((gun, internalBattery)), Is.Zero);

        var battery = SSpawn("TestMechBattery");
        _mech.InsertBattery(mech, battery, component);
        _mech.InsertEquipment(mech, gun, component);
        Assert.That(_container.Remove(gun, component.EquipmentContainer, force: true), Is.True);
        SComp<MechEquipmentComponent>(gun).EquipmentOwner = mech;
        await OverrideCVar(Side.Server, CCVars.MechGunOutsideMech, false);
        Assert.That(TakeAmmo(gun, 1), Is.Empty);
        Assert.That(component.Energy, Is.EqualTo(FixedPoint2.New(100)));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task SelectionValidationAndCyclingStopStaleGun()
    {
        var mech = SSpawn("TestMech");
        var pilot = SSpawn("TestMechPilot");
        var first = SSpawn("TestMechGun");
        var second = SSpawn("TestMechGun");
        var component = SComp<MechComponent>(mech);

        _mech.InsertEquipment(mech, first, component);
        _mech.InsertEquipment(mech, second, component);
        _mech.CycleEquipment(mech, component);
        Assert.That(component.CurrentSelectedEquipment, Is.EqualTo(first));
        Assert.That(_vehicle.TryEnter(mech, pilot), Is.True);
        Assert.That(_gun.TryGetGun(pilot, out var selected), Is.True);
        Assert.That(selected.Owner, Is.EqualTo(first));

        _mech.CycleEquipment(mech, component);
        Assert.That(component.CurrentSelectedEquipment, Is.EqualTo(second));

        SComp<MechEquipmentComponent>(second).EquipmentOwner = null;
        Assert.That(_gun.TryGetGun(pilot, out _), Is.False);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task EquipmentClassesRestrictExistingMechs()
    {
        var ripley = SSpawn("MechRipley");
        var honker = SSpawn("MechHonker");
        var hamtr = SSpawn("MechHamtr");
        var combat = SSpawn("TestCombatMech");

        AssertEquipmentAccepted(ripley, "MechEquipmentGrabber");
        AssertEquipmentRejected(ripley, "MechEquipmentHorn");
        AssertEquipmentAccepted(honker, "MechEquipmentHorn");
        AssertEquipmentRejected(honker, "MechEquipmentGrabber");
        AssertEquipmentAccepted(hamtr, "MechEquipmentGrabberSmall");
        AssertEquipmentRejected(hamtr, "MechEquipmentGrabber");
        AssertEquipmentAccepted(combat, "TestCombatMechEquipment");

        AssertEquipmentAccepted(ripley, "TestDebugMechEquipment");
        AssertEquipmentAccepted(honker, "TestDebugMechEquipment");
        AssertEquipmentAccepted(hamtr, "TestDebugMechEquipment");
        AssertEquipmentAccepted(combat, "TestDebugMechEquipment");
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task GrabberPreservesEnergyContentsAndRemovalPolicy()
    {
        var map = await Pair.CreateTestMap();
        var mech = SEntMan.SpawnEntity("TestMech", map.GridCoords);
        var battery = SEntMan.SpawnEntity("TestMechBattery", map.GridCoords);
        var grabber = SEntMan.SpawnEntity("TestMechGrabber", map.GridCoords);
        var target = SEntMan.SpawnEntity("TestMechGrabTarget", map.GridCoords);
        var component = SComp<MechComponent>(mech);
        var grabberComponent = SComp<MechGrabberComponent>(grabber);

        _mech.InsertBattery(mech, battery, component);
        _mech.InsertEquipment(mech, grabber, component);
        var activate = new UserActivateInWorldEvent(mech, target, true);
        SEntMan.EventBus.RaiseLocalEvent(grabber, activate);
        await Pair.Server.WaitRunTicks(1);

        Assert.Multiple(() =>
        {
            Assert.That(grabberComponent.ItemContainer.Contains(target), Is.True);
            Assert.That(component.Energy, Is.EqualTo(FixedPoint2.New(70)));
        });

        _mech.RemoveEquipment(mech, grabber, component);
        Assert.That(SComp<MechEquipmentComponent>(grabber).EquipmentOwner, Is.EqualTo(mech));

        SEntMan.DeleteEntity(target);
        await Pair.Server.WaitRunTicks(1);
        Assert.That(grabberComponent.ItemContainer.ContainedEntities, Is.Empty);

        var otherGrabber = SEntMan.SpawnEntity("TestMechGrabber", map.GridCoords);
        var otherContainer = SComp<MechGrabberComponent>(otherGrabber).ItemContainer;
        var containedElsewhere = SEntMan.SpawnEntity("TestMechGrabTarget", map.GridCoords);
        Assert.That(_container.Insert(containedElsewhere, otherContainer), Is.True);
        activate = new UserActivateInWorldEvent(mech, containedElsewhere, true);
        SEntMan.EventBus.RaiseLocalEvent(grabber, activate);
        await Pair.Server.WaitRunTicks(1);
        Assert.That(grabberComponent.ItemContainer.Contains(containedElsewhere), Is.False);

        var blocked = SEntMan.SpawnEntity("TestMechGrabBlockedTarget", map.GridCoords);
        activate = new UserActivateInWorldEvent(mech, blocked, true);
        SEntMan.EventBus.RaiseLocalEvent(grabber, activate);
        var otherMech = SEntMan.SpawnEntity("TestMech", map.GridCoords);
        activate = new UserActivateInWorldEvent(mech, otherMech, true);
        SEntMan.EventBus.RaiseLocalEvent(grabber, activate);
        await Pair.Server.WaitRunTicks(1);
        Assert.Multiple(() =>
        {
            Assert.That(grabberComponent.ItemContainer.Contains(blocked), Is.False);
            Assert.That(grabberComponent.ItemContainer.Contains(otherMech), Is.False);
            Assert.That(component.Energy, Is.EqualTo(FixedPoint2.New(70)));
        });

        var firstCapacityTarget = SEntMan.SpawnEntity("TestMechGrabTarget", map.GridCoords);
        var secondCapacityTarget = SEntMan.SpawnEntity("TestMechGrabTarget", map.GridCoords);
        Assert.That(_container.Insert(firstCapacityTarget, grabberComponent.ItemContainer), Is.True);
        Assert.That(_container.Insert(secondCapacityTarget, grabberComponent.ItemContainer), Is.True);
        var overCapacityTarget = SEntMan.SpawnEntity("TestMechGrabTarget", map.GridCoords);
        activate = new UserActivateInWorldEvent(mech, overCapacityTarget, true);
        SEntMan.EventBus.RaiseLocalEvent(grabber, activate);
        await Pair.Server.WaitRunTicks(1);
        Assert.That(grabberComponent.ItemContainer.Contains(overCapacityTarget), Is.False);
        Assert.That(_container.Remove(firstCapacityTarget, grabberComponent.ItemContainer), Is.True);
        Assert.That(_container.Remove(secondCapacityTarget, grabberComponent.ItemContainer), Is.True);

        component.Energy = FixedPoint2.New(29);
        var unaffordableTarget = SEntMan.SpawnEntity("TestMechGrabTarget", map.GridCoords);
        activate = new UserActivateInWorldEvent(mech, unaffordableTarget, true);
        SEntMan.EventBus.RaiseLocalEvent(grabber, activate);
        await Pair.Server.WaitRunTicks(1);
        Assert.Multiple(() =>
        {
            Assert.That(grabberComponent.ItemContainer.Contains(unaffordableTarget), Is.False);
            Assert.That(component.Energy, Is.EqualTo(FixedPoint2.New(29)));
        });

        var forcedTarget = SEntMan.SpawnEntity("TestMechGrabTarget", map.GridCoords);
        Assert.That(_container.Insert(forcedTarget, grabberComponent.ItemContainer), Is.True);
        _mech.RemoveEquipment(mech, grabber, component, forced: true);
        Assert.Multiple(() =>
        {
            Assert.That(grabberComponent.ItemContainer.ContainedEntities, Is.Empty);
            Assert.That(_container.IsEntityInContainer(forcedTarget), Is.False);
            Assert.That(SComp<MechEquipmentComponent>(grabber).EquipmentOwner, Is.Null);
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task AllMechEquipmentPrototypesSpawnWithExpectedBehaviorComponents()
    {
        string[] ranged =
        [
            "WeaponMechCombatPulseRifle", "WeaponMechCombatImmolationGun", "WeaponMechCombatSolarisLaser",
            "WeaponMechCombatFiredartLaser", "WeaponMechCombatTeslaCannon", "WeaponMechCombatDisabler",
            "WeaponMechCombatTaser", "WeaponMechCombatShotgun", "WeaponMechCombatShotgunIncendiary",
            "WeaponMechCombatUltraRifle", "WeaponMechCombatMissileRack8", "WeaponMechCombatMissileRack6",
            "WeaponMechCombatFlashbangLauncher", "WeaponMechIndustrialKineticAccelerator",
            "WeaponMechSpecialMousetrapMortar", "WeaponMechSpecialBananaMortar",
            "WeaponMechDebugBallistic", "WeaponMechDebugLaser", "WeaponMechDebugDisabler"
        ];
        string[] melee =
        [
            "WeaponMechMeleeDrill", "WeaponMechMeleeDrillDiamond", "WeaponMechChainSword", "WeaponMechDebugMelle"
        ];

        foreach (var id in ranged)
        {
            var equipment = SSpawn(id);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<MechEquipmentComponent>(equipment), Is.True, id);
                Assert.That(SEntMan.HasComponent<GunComponent>(equipment), Is.True, id);
                Assert.That(SEntMan.HasComponent<BatteryAmmoProviderComponent>(equipment), Is.True, id);
            });
        }

        foreach (var id in melee)
        {
            var equipment = SSpawn(id);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<MechEquipmentComponent>(equipment), Is.True, id);
                Assert.That(SEntMan.HasComponent<Content.Shared.Weapons.Melee.MeleeWeaponComponent>(equipment), Is.True, id);
            });
        }
    }

    private List<(EntityUid? Entity, IShootable Shootable)> TakeAmmo(EntityUid gun, int shots)
    {
        var ammo = new List<(EntityUid? Entity, IShootable Shootable)>();
        SEntMan.EventBus.RaiseLocalEvent(gun,
            new TakeAmmoEvent(shots, ammo, new EntityCoordinates(gun, 0, 0), null));
        return ammo;
    }

    private void AssertEquipmentAccepted(EntityUid mech, string prototype)
    {
        var equipment = SSpawn(prototype);
        var component = SComp<MechEquipmentComponent>(equipment);
        _mech.InsertEquipment(mech, equipment, equipmentComponent: component);
        Assert.That(component.EquipmentOwner, Is.EqualTo(mech), $"{prototype} should fit mech {mech}");
    }

    private void AssertEquipmentRejected(EntityUid mech, string prototype)
    {
        var equipment = SSpawn(prototype);
        var component = SComp<MechEquipmentComponent>(equipment);
        _mech.InsertEquipment(mech, equipment, equipmentComponent: component);
        Assert.That(component.EquipmentOwner, Is.Null, $"{prototype} should not fit mech {mech}");
    }

    private EmpPulseEvent RaiseEmp(EntityUid mech, float energy)
    {
        var emp = new EmpPulseEvent(energy, false, false, TimeSpan.Zero, null);
        SEntMan.EventBus.RaiseLocalEvent(mech, ref emp);
        return emp;
    }

    private void AssertPilotInserted(EntityUid mech, EntityUid pilot, MechComponent component)
    {
        Assert.Multiple(() =>
        {
            Assert.That(SComp<VehicleComponent>(mech).Operator, Is.EqualTo(pilot));
            Assert.That(SComp<VehicleOperatorComponent>(pilot).Vehicle, Is.EqualTo(mech));
            Assert.That(SComp<VehicleComponent>(mech).Operator, Is.EqualTo(pilot));
            Assert.That(SComp<RelayInputMoverComponent>(pilot).RelayEntity, Is.EqualTo(mech));
            Assert.That(SComp<MovementRelayTargetComponent>(mech).Source, Is.EqualTo(pilot));
            Assert.That(SComp<InteractionRelayComponent>(pilot).RelayEntity, Is.EqualTo(mech));
        });
    }

    private void AssertPilotRemoved(EntityUid mech, EntityUid pilot, MechComponent component)
    {
        Assert.Multiple(() =>
        {
            Assert.That(SComp<VehicleComponent>(mech).Operator, Is.Null);
            Assert.That(_container.IsEntityInContainer(pilot), Is.False);
            Assert.That(SEntMan.HasComponent<VehicleOperatorComponent>(pilot), Is.False);
            Assert.That(SComp<VehicleComponent>(mech).Operator, Is.Null);
            Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(pilot), Is.False);
            Assert.That(SEntMan.HasComponent<MovementRelayTargetComponent>(mech), Is.False);
            Assert.That(SEntMan.HasComponent<InteractionRelayComponent>(pilot), Is.False);
        });
    }
}
