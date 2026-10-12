// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System;
using Content.Shared.Actions;
using Content.IntegrationTests.Fixtures;
using Content.Server._Onyx.Clothing.Modsuits.Systems;
using Content.Shared._Onyx.Clothing.Components;
using Content.Shared._Onyx.Clothing.Systems;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Gravity;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared._Onyx.MartialArts;
using Content.Shared.Damage;
using Content.Shared.Atmos.Components;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests._Onyx.Clothing;

[TestFixture]
public sealed class ModModuleGravityTest : GameTest
{
    [Test]
    public async Task AtrocinatorStartsReadyAndPreservesCooldownAcrossResealing()
    {
        var server = Pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var controller = entities.SpawnEntity("ClothingModsuitStandardPowerCell", map.GridCoords);
            var module = entities.SpawnEntity("AtrocinatorSuitModule", map.GridCoords);
            var containers = entities.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(controller, ModModuleContainerComponent.ContainerId, out var modules), Is.True);
            Assert.That(containers.Insert(module, modules, force: true), Is.True);
            EquipAndSeal(entities, wearer, controller);
            var framework = entities.GetComponent<ModModuleComponent>(module);
            var actionUid = framework.ActionEntities["ActionActivateSuitModuleAtrocinator"];
            var action = entities.GetComponent<ActionComponent>(actionUid);
            var actions = entities.System<SharedActionsSystem>();
            Assert.That(actions.IsCooldownActive(action), Is.False);
            actions.SetCooldown(actionUid, TimeSpan.FromSeconds(300));
            var cooldown = action.Cooldown;

            var control = new Entity<SealableClothingControlComponent>(controller,
                entities.GetComponent<SealableClothingControlComponent>(controller));
            var sealing = entities.System<SharedSealableClothingSystem>();
            var parts = entities.System<ToggleableClothingSystem>().GetAttachedClothingsList(controller);
            Assert.That(parts, Is.Not.Null.And.Not.Empty);
            for (var cycle = 0; cycle < 2; cycle++)
            {
                foreach (var part in parts!)
                    Assert.That(sealing.SealPart(part, control, silent: true), Is.True);
                sealing.EndSealProcess(control, silent: true);
                Assert.That(action.AttachedEntity, Is.Null);
                foreach (var part in parts!)
                    Assert.That(sealing.SealPart(part, control, silent: true), Is.True);
                sealing.EndSealProcess(control, silent: true);
                Assert.That(framework.ActionEntities["ActionActivateSuitModuleAtrocinator"], Is.EqualTo(actionUid));
                Assert.That(action.AttachedEntity, Is.EqualTo(wearer));
                Assert.That(action.Cooldown, Is.EqualTo(cooldown));
                Assert.That(actions.IsCooldownActive(action), Is.True);
            }
        });
    }

    [Test]
    public async Task ShieldBlocksFullDamageIncludingLastHit()
    {
        var server = Pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var shield = entities.AddComponent<ModModuleEnergyShieldEffectComponent>(wearer);
            shield.Effect = entities.SpawnEntity("EnergyShieldEffect", map.GridCoords);
            shield.SustainingCount = 1;
            var modifiers = new List<DamageModifierSet>();
            var ev = new GetMeleeTargetModifiersEvent(modifiers);
            entities.EventBus.RaiseLocalEvent(wearer, ref ev);
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 25;
            damage.DamageDict["Heat"] = 10;
            Assert.That(DamageSpecifier.ApplyModifierSets(damage, modifiers).Empty, Is.True);
            Assert.That(entities.HasComponent<ModModuleEnergyShieldEffectComponent>(wearer), Is.False);
        });
    }

    [Test]
    public async Task LastChargeDoesNotDeactivateModuleInsideAction()
    {
        var server = Pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();
        EntityUid wearer = default;
        EntityUid module = default;

        await server.WaitAssertion(() =>
        {
            wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var controller = entities.SpawnEntity("ClothingModsuitStandardPowerCell", map.GridCoords);
            EquipAndSeal(entities, wearer, controller);
            var bay = entities.GetComponent<ModModuleContainerComponent>(controller);
            var containers = entities.System<SharedContainerSystem>();
            module = entities.SpawnEntity("AntiGravitySuitModule", map.GridCoords);
            Assert.That(containers.TryGetContainer(controller, ModModuleContainerComponent.ContainerId, out var modules), Is.True);
            Assert.That(containers.Insert(module, modules, force: true), Is.True);
            var core = entities.SpawnEntity("AnomalyCoreGravity", map.GridCoords);
            Assert.That(containers.TryGetContainer(module, "module_core", out var coreSlot), Is.True);
            Assert.That(containers.Insert(core, coreSlot), Is.True);
            Assert.That(entities.System<ModModuleSystem>().TryToggle(module, (controller, bay), wearer), Is.True);
            var power = entities.System<Content.Shared.PowerCell.PowerCellSystem>();
            Assert.That(power.TryGetBatteryFromSlot(controller, out var battery), Is.True);
            var charge = entities.System<SharedBatterySystem>().GetCharge(battery.Value.Owner).Charge;
            Assert.That(power.TryUseCharge(controller, charge), Is.True);
            Assert.That(bay.Powered, Is.False);
            Assert.That(entities.GetComponent<ModModuleComponent>(module).Active, Is.True);
            Assert.That(entities.System<ModModuleSystem>().CanUseModule(module, wearer, out _), Is.False);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<ModModuleComponent>(module).Active, Is.False);
            Assert.That(entities.HasComponent<ModModuleAntiGravityEffectComponent>(wearer), Is.False);
        });
    }

    [Test]
    public async Task AntiGravityRequiresCoreAndCleansUpWhenCoreIsRemoved()
    {
        var server = Pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var controller = entities.SpawnEntity("ClothingModsuitStandardPowerCell", map.GridCoords);
            var bay = entities.GetComponent<ModModuleContainerComponent>(controller);
            EquipAndSeal(entities, wearer, controller);
            var module = entities.SpawnEntity("AntiGravitySuitModule", map.GridCoords);
            var framework = entities.GetComponent<ModModuleComponent>(module);
            var containers = entities.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(controller, ModModuleContainerComponent.ContainerId, out var modules), Is.True);
            Assert.That(containers.Insert(module, modules, force: true), Is.True);
            var system = entities.System<ModModuleSystem>();
            Assert.That(system.TryToggle(module, (controller, bay), wearer), Is.True);
            Assert.That(framework.Active, Is.False);
            Assert.That(entities.HasComponent<ModModuleAntiGravityEffectComponent>(wearer), Is.False);

            var core = entities.SpawnEntity("AnomalyCoreGravity", map.GridCoords);
            Assert.That(containers.TryGetContainer(module, "module_core", out var coreSlot), Is.True);
            Assert.That(containers.Insert(core, coreSlot), Is.True);
            Assert.That(framework.Active, Is.True);
            Assert.That(framework.ToggleAction, Is.Not.Null);
            var action = entities.GetComponent<ActionComponent>(framework.ToggleAction!.Value);
            Assert.That(action.AttachedEntity, Is.EqualTo(wearer));
            Assert.That(action.ItemIconStyle, Is.EqualTo(ItemActionIconStyle.NoItem));
            Assert.That(system.CanUseModule(module, wearer, out _), Is.True);
            Assert.That(entities.HasComponent<ModModuleAntiGravityEffectComponent>(wearer), Is.True);
            Assert.That(containers.Remove(core, coreSlot), Is.True);
            Assert.That(framework.Active, Is.False);
            Assert.That(system.CanUseModule(module, wearer, out _), Is.False);
            Assert.That(entities.HasComponent<ModModuleAntiGravityEffectComponent>(wearer), Is.False);
            var control = new Entity<SealableClothingControlComponent>(controller,
                entities.GetComponent<SealableClothingControlComponent>(controller));
            var sealing = entities.System<SharedSealableClothingSystem>();
            var parts = entities.System<ToggleableClothingSystem>().GetAttachedClothingsList(controller);
            Assert.That(parts, Is.Not.Null.And.Not.Empty);
            foreach (var part in parts!)
                Assert.That(sealing.SealPart(part, control, silent: true), Is.True);
            sealing.EndSealProcess(control, silent: true);
            Assert.That(action.AttachedEntity, Is.Null);
        });
    }

    [Test]
    public async Task ToggleValidationDoesNotChangeModuleState()
    {
        var server = Pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var controller = entities.SpawnEntity("ClothingModsuitStandardPowerCell", map.GridCoords);
            var bay = entities.GetComponent<ModModuleContainerComponent>(controller);
            EquipAndSeal(entities, wearer, controller);
            bay.Powered = false;
            var module = entities.SpawnEntity(null, map.GridCoords);
            var framework = entities.AddComponent<ModModuleComponent>(module);
            framework.InstalledController = controller;
            framework.Toggleable = true;
            framework.Enabled = true;
            var system = entities.System<ModModuleSystem>();

            Assert.That(system.CanToggle(module, (controller, bay), wearer, out _), Is.True);
            Assert.That(framework.Enabled, Is.True);
            framework.Toggleable = false;
            Assert.That(system.CanToggle(module, (controller, bay), wearer, out _), Is.False);
            framework.Toggleable = true;
            framework.Enabled = false;
            Assert.That(system.CanToggle(module, (controller, bay), wearer, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("mod-module-error-enable-power"));
            Assert.That(framework.Enabled, Is.False);
            bay.Powered = true;
            Assert.That(system.CanToggle(module, (controller, bay), wearer, out _), Is.True);
            framework.Permanent = true;
            Assert.That(system.CanToggle(module, (controller, bay), wearer, out _), Is.False);
            Assert.That(framework.Enabled, Is.False);
        });
    }

    private static void EquipAndSeal(IEntityManager entities, EntityUid wearer, EntityUid controller)
    {
        Assert.That(entities.System<InventorySystem>().TryEquip(wearer, controller, "back"), Is.True);
        var control = new Entity<SealableClothingControlComponent>(controller,
            entities.GetComponent<SealableClothingControlComponent>(controller));
        var sealing = entities.System<SharedSealableClothingSystem>();
        var parts = entities.System<ToggleableClothingSystem>().GetAttachedClothingsList(controller);
        Assert.That(parts, Is.Not.Null.And.Not.Empty);
        foreach (var part in parts!)
            Assert.That(sealing.SealPart(part, control, silent: true), Is.True);
        sealing.EndSealProcess(control, silent: true);
        Assert.That(control.Comp.WearerEntity, Is.EqualTo(wearer));
        Assert.That(control.Comp.IsCurrentlySealed, Is.True);
    }

    [Test]
    public async Task AntiGravityAndMagneticEffectsRestoreGridGravity()
    {
        var server = Pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();
        EntityUid wearer = default;

        await server.WaitPost(() =>
        {
            wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.AddComponent<ModModuleAntiGravityEffectComponent>(wearer);
        });

        await server.WaitAssertion(() =>
            Assert.That(entities.System<SharedGravitySystem>().IsWeightless(wearer), Is.True));

        await server.WaitPost(() =>
        {
            var pressure = entities.EnsureComponent<MovedByPressureComponent>(wearer);
            var magnetic = entities.AddComponent<ModModuleMagneticEffectComponent>(wearer);
            magnetic.OriginalPressureEnabled = pressure.Enabled;
            pressure.Enabled = false;
        });
        await server.WaitAssertion(() =>
            Assert.That(entities.System<SharedGravitySystem>().IsWeightless(wearer), Is.False));

        await server.WaitPost(() => entities.RemoveComponent<ModModuleMagneticEffectComponent>(wearer));
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<SharedGravitySystem>().IsWeightless(wearer), Is.True);
            Assert.That(entities.GetComponent<MovedByPressureComponent>(wearer).Enabled, Is.True);
        });

        await server.WaitPost(() => entities.RemoveComponent<ModModuleAntiGravityEffectComponent>(wearer));
        await server.WaitAssertion(() =>
            Assert.That(entities.System<SharedGravitySystem>().IsWeightless(wearer),
                Is.EqualTo(!entities.System<SharedGravitySystem>().EntityGridOrMapHaveGravity(wearer))));
    }
}
