using System.Linq;
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Wounds;
using Content.Shared._Onyx.Targeting;
using Content.Shared.Armor;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.EntityEffects;
using Content.Shared.Rejuvenate;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

#pragma warning disable CS0618 // These tests intentionally verify the legacy Damageable projection maintained by WoundDamageRoutingSystem.
[TestFixture]
[TestOf(typeof(WoundDamageRoutingSystem))]
public sealed class WoundDamageFoundationTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WoundFoundationBody
  parent: InventoryBase
  components:
  - type: Body
  - type: Sprite
  - type: Damageable
  - type: Injurable
    damageContainer: Biological
  - type: WoundHost
  - type: InitialBody
    organs:
      Torso: WoundFoundationTorso
      Head: WoundFoundationHead
      ArmLeft: WoundFoundationLeftArm
      ArmRight: WoundFoundationRightArm

- type: entity
  id: WoundFoundationTorso
  components:
  - type: BodyPart
    partType: Torso

- type: entity
  id: WoundFoundationHead
  components:
  - type: BodyPart
    partType: Head

- type: entity
  id: WoundFoundationLeftArm
  components:
  - type: BodyPart
    partType: Arm
    symmetry: Left

- type: entity
  id: WoundFoundationRightArm
  components:
  - type: BodyPart
    partType: Arm
    symmetry: Right

- type: entity
  id: WoundFoundationArmorHead
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Head]
    modifiers:
      coefficients:
        Blunt: 0.5

- type: entity
  id: WoundFoundationArmorAll
  components:
  - type: Clothing
    slots: [head]
  - type: Armor
    modifiers:
      coefficients:
        Blunt: 0.5

- type: entity
  id: WoundFoundationArmorLeftArm
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Arm]
    coverageSymmetry: [Left]
    modifiers:
      coefficients:
        Blunt: 0.5

- type: entity
  id: WoundFoundationArmorLocational
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Torso]
    modifiers:
      coefficients:
        Blunt: 0.8
    partModifiers:
    - parts: [Head]
      modifiers:
        coefficients:
          Blunt: 0.25
    - parts: [Arm]
      symmetry: [Left]
      modifiers:
        coefficients:
          Blunt: 0.5
    - parts: [Arm]
      symmetry: [Right]
      modifiers:
        coefficients:
          Blunt: 0.75

- type: entity
  id: WoundFoundationVanillaBody
  parent: InventoryBase
  components:
  - type: Sprite
  - type: Damageable
  - type: Injurable
    damageContainer: Biological

- type: entity
  id: WoundFoundationAttacker
  components:
  - type: Targeting

";

    [Test]
    public async Task TargetingContractAndRoutingTest()
    {
        Assert.That(SharedTargetingSystem.TryConvert(TargetBodyPart.Chest, out var chest, out _), Is.True);
        Assert.That(chest, Is.EqualTo(BodyPartType.Torso));
        Assert.That(SharedTargetingSystem.TryConvert(TargetBodyPart.Groin, out var groin, out _), Is.True);
        Assert.That(groin, Is.EqualTo(BodyPartType.Torso));
        Assert.That(SharedTargetingSystem.IsSelectable((TargetBodyPart) ushort.MaxValue), Is.False);
        Assert.That(SharedTargetingSystem.IsSelectable(TargetBodyPart.Arms), Is.False);
        Assert.That(TargetingComponent.DefaultOdds()[TargetBodyPart.RightFoot].Keys,
            Is.EquivalentTo(new[] { TargetBodyPart.RightFoot, TargetBodyPart.RightLeg }));
        Assert.That(TargetingComponent.DefaultOdds()[TargetBodyPart.LeftHand].Keys,
            Is.EquivalentTo(new[] { TargetBodyPart.LeftHand, TargetBodyPart.LeftArm }));
        Assert.That(TargetingComponent.DefaultOdds()[TargetBodyPart.RightArm].Keys,
            Is.EquivalentTo(new[] { TargetBodyPart.RightArm, TargetBodyPart.RightHand, TargetBodyPart.Chest }));
        Assert.That(TargetingComponent.DefaultOdds()[TargetBodyPart.Chest].Keys,
            Is.EquivalentTo(new[]
            {
                TargetBodyPart.Chest,
                TargetBodyPart.Head,
                TargetBodyPart.LeftArm,
                TargetBodyPart.RightArm,
                TargetBodyPart.LeftLeg,
                TargetBodyPart.RightLeg,
            }));

        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            configuration.SetCVar(CCVars.TargetingEnabled, true);
            configuration.SetCVar(CCVars.TargetingUseAnatomicalOdds, false);
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var attacker = entityManager.SpawnEntity("WoundFoundationAttacker", map.GridCoords);
            var targeting = entityManager.GetComponent<TargetingComponent>(attacker);
            var resolver = entityManager.System<TargetResolverSystem>();
            var graph = entityManager.System<SharedBodySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var leftArm = parts.Single(part => part.Component.PartType == BodyPartType.Arm &&
                                               part.Component.Symmetry == BodyPartSymmetry.Left).Id;

            targeting.Target = TargetBodyPart.Head;
            Assert.That(resolver.TryResolve(body, attacker, out var resolvedHead), Is.True);
            Assert.That(resolvedHead, Is.EqualTo(head));

            targeting.Target = TargetBodyPart.LeftHand;
            Assert.That(resolver.TryResolve(body, attacker, out var resolvedHand), Is.True);
            Assert.That(resolvedHand, Is.EqualTo(leftArm));
            Assert.That(routing.TryApplyDamage(body, Spec("Blunt", 10), attacker), Is.True);
            Assert.That(damage.GetAllDamage(leftArm).GetTotal(), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(damage.GetAllDamage(head).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
        });

        await server.WaitPost(() =>
        {
            configuration.SetCVar(CCVars.TargetingEnabled, CCVars.TargetingEnabled.DefaultValue);
            configuration.SetCVar(CCVars.TargetingUseAnatomicalOdds, CCVars.TargetingUseAnatomicalOdds.DefaultValue);
        });
    }

    [Test]
    public async Task CombatSnapshotsRemainStableTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            configuration.SetCVar(CCVars.TargetingEnabled, true);
            configuration.SetCVar(CCVars.TargetingUseAnatomicalOdds, false);
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var shooter = entityManager.SpawnEntity("WoundFoundationAttacker", map.GridCoords);
            var first = entityManager.SpawnEntity(null, map.GridCoords);
            var spread = entityManager.SpawnEntity(null, map.GridCoords);
            var thrown = entityManager.SpawnEntity(null, map.GridCoords);
            var targeting = entityManager.GetComponent<TargetingComponent>(shooter);
            var snapshots = entityManager.System<TargetingSnapshotSystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var graph = entityManager.System<SharedBodySystem>();
            var damage = entityManager.System<DamageableSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var leftArm = parts.Single(part => part.Component.PartType == BodyPartType.Arm &&
                                               part.Component.Symmetry == BodyPartSymmetry.Left).Id;

            targeting.Target = TargetBodyPart.Head;
            Assert.That(snapshots.Capture(first, shooter));
            Assert.That(snapshots.Capture(spread, shooter));
            var thrownEvent = new ThrownEvent(shooter, thrown);
            entityManager.EventBus.RaiseLocalEvent(thrown, ref thrownEvent, true);

            targeting.Target = TargetBodyPart.LeftArm;
            Assert.That(entityManager.GetComponent<TargetingSnapshotComponent>(first).RequestedTarget, Is.EqualTo(TargetBodyPart.Head));
            Assert.That(entityManager.GetComponent<TargetingSnapshotComponent>(spread).RequestedTarget, Is.EqualTo(TargetBodyPart.Head));
            Assert.That(entityManager.GetComponent<TargetingSnapshotComponent>(thrown).RequestedTarget, Is.EqualTo(TargetBodyPart.Head));
            Assert.That(routing.TryApplyCarrierDamage(body, first, Spec("Blunt", 4), shooter, out _));
            Assert.That(routing.TryApplyCarrierDamage(body, spread, Spec("Blunt", 3), shooter, out _));
            Assert.That(routing.TryApplyCarrierDamage(body, thrown, Spec("Blunt", 2), shooter, out _));
            Assert.That(damage.GetAllDamage(head).GetTotal(), Is.EqualTo(FixedPoint2.New(9)));
            Assert.That(damage.GetAllDamage(leftArm).GetTotal(), Is.EqualTo(FixedPoint2.Zero));

            Assert.That(routing.TryApplyTargetedDamage(body, Spec("Blunt", 1), TargetBodyPart.LeftArm, shooter, out _));
            Assert.That(damage.GetAllDamage(leftArm).GetTotal(), Is.EqualTo(FixedPoint2.New(1)));
        });

        await server.WaitPost(() =>
        {
            configuration.SetCVar(CCVars.TargetingEnabled, CCVars.TargetingEnabled.DefaultValue);
            configuration.SetCVar(CCVars.TargetingUseAnatomicalOdds, CCVars.TargetingUseAnatomicalOdds.DefaultValue);
        });
    }

    [Test]
    public async Task RoutesAndProjectsDamageTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;

            Assert.That(entityManager.HasComponent<WoundableComponent>(head));
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 10)));
            Assert.That(damage.GetAllDamage(head).GetTotal(), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(damage.GetAllDamage(torso).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(damage.GetAllDamage(body).GetTotal(), Is.EqualTo(FixedPoint2.New(10)));

            Assert.That(routing.TryApplyDamage(body, Spec("Asphyxiation", 4)));
            Assert.That(entityManager.GetComponent<SystemicDamageComponent>(body).Damage.GetTotal(), Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(damage.GetAllDamage(body).GetTotal(), Is.EqualTo(FixedPoint2.New(14)));

            Assert.That(graph.TryDetachPart(head));
            Assert.That(damage.GetAllDamage(body).GetTotal(), Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 1)), Is.False);
        });
    }

    [Test]
    public async Task NonTargetingOriginUsesWeightedFallbackTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var source = entityManager.SpawnEntity(null, map.GridCoords);
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var parts = entityManager.System<SharedBodySystem>().GetBodyChildren(body).ToList();

            Assert.That(routing.TryApplyDamage(body, Spec("Blunt", 10), source), Is.True);
            var partTotal = parts.Aggregate(FixedPoint2.Zero,
                (total, part) => total + damage.GetAllDamage(part.Id).GetTotal());
            Assert.That(partTotal, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(damage.GetAllDamage(body).GetTotal(), Is.EqualTo(FixedPoint2.New(10)));
        });
    }

    [Test]
    public async Task DistributedDamageMasksAndRoundingTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var attacker = entityManager.SpawnEntity("WoundFoundationAttacker", map.GridCoords);
            entityManager.GetComponent<TargetingComponent>(attacker).Target = TargetBodyPart.Head;
            var graph = entityManager.System<SharedBodySystem>();
            var resolver = entityManager.System<TargetResolverSystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var leftArm = parts.Single(part => part.Component.PartType == BodyPartType.Arm &&
                                               part.Component.Symmetry == BodyPartSymmetry.Left).Id;
            var rightArm = parts.Single(part => part.Component.PartType == BodyPartType.Arm &&
                                                part.Component.Symmetry == BodyPartSymmetry.Right).Id;

            Assert.That(resolver.GetMatchingParts(body, TargetBodyPart.Chest | TargetBodyPart.Groin), Is.EqualTo(new[] { torso }));
            Assert.That(resolver.GetMatchingParts(body, TargetBodyPart.FullArms).ToHashSet(),
                Is.EqualTo(new HashSet<EntityUid> { leftArm, rightArm }));
            Assert.That(resolver.GetMatchingParts(body, TargetBodyPart.All).Count, Is.EqualTo(4));

            var distributed = Spec("Blunt", 10);
            distributed.DamageDict[new ProtoId<DamageTypePrototype>("Heat")] = FixedPoint2.New(7);
            distributed.DamageDict[new ProtoId<DamageTypePrototype>("Asphyxiation")] = FixedPoint2.New(4);
            Assert.That(routing.TryApplyDistributedDamage(body,
                distributed,
                TargetBodyPart.All,
                DamageDistribution.SplitByPartWeight,
                attacker));
            Assert.That(parts.Select(part => damage.GetAllDamage(part.Id).DamageDict.GetValueOrDefault(new ProtoId<DamageTypePrototype>("Blunt"))).Sum(),
                Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(parts.Select(part => damage.GetAllDamage(part.Id).DamageDict.GetValueOrDefault(new ProtoId<DamageTypePrototype>("Heat"))).Sum(),
                Is.EqualTo(FixedPoint2.New(7)));
            Assert.That(damage.GetAllDamage(head).DamageDict.GetValueOrDefault(new ProtoId<DamageTypePrototype>("Blunt")),
                Is.LessThan(FixedPoint2.New(10)));
            Assert.That(entityManager.GetComponent<SystemicDamageComponent>(body).Damage.DamageDict[new ProtoId<DamageTypePrototype>("Asphyxiation")],
                Is.EqualTo(FixedPoint2.New(4)));

            Assert.That(graph.TryDetachPart(head));
            Assert.That(resolver.GetMatchingParts(body, TargetBodyPart.Vital), Is.EqualTo(new[] { torso }));
            Assert.That(routing.TryApplyDistributedDamage(body,
                Spec("Slash", 1),
                TargetBodyPart.Vital,
                DamageDistribution.SplitEvenly));
            Assert.That(damage.GetAllDamage(torso).DamageDict.GetValueOrDefault(new ProtoId<DamageTypePrototype>("Slash")), Is.EqualTo(FixedPoint2.New(1)));
            Assert.That(damage.GetAllDamage(head).DamageDict.GetValueOrDefault(new ProtoId<DamageTypePrototype>("Slash")), Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task CreatesMergesHealsAndPreservesWoundsTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var wounds = entityManager.System<WoundSystem>();
            var head = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Head).Id;

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 10)));
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 5)));
            var wound = wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head)))
                .Single(candidate => candidate.Comp.Prototype == new ProtoId<WoundPrototype>("BluntWound"));
            Assert.That(wound.Comp.Prototype, Is.EqualTo(new ProtoId<WoundPrototype>("BluntWound")));
            Assert.That(wound.Comp.Severity, Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(wound.Comp.PeakSeverity, Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(wound.Comp.HoldingPart, Is.EqualTo(head));

            Assert.That(graph.TryDetachPart(head));
            Assert.That(wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head)))
                .Single(candidate => candidate.Comp.Prototype == new ProtoId<WoundPrototype>("BluntWound")).Owner, Is.EqualTo(wound.Owner));
            var torso = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Torso).Id;
            Assert.That(graph.TryAttachPart(torso, head));
            Assert.That(wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head)))
                .Single(candidate => candidate.Comp.Prototype == new ProtoId<WoundPrototype>("BluntWound")).Owner, Is.EqualTo(wound.Owner));

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", -15)));
            Assert.That(wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head))), Is.Empty);

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 4)));
            entityManager.EventBus.RaiseLocalEvent(body, new RejuvenateEvent());
            Assert.That(wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head))), Is.Empty);
        });
    }

    [Test]
    public async Task AppliesLocationalArmorExactlyOnceTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var armor = entityManager.SpawnEntity("WoundFoundationArmorHead", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var inventory = entityManager.System<InventorySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;

            Assert.That(inventory.TryEquip(body, armor, "outerClothing"), Is.True);
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 10)));
            Assert.That(routing.TryApplyPartDamage(body, torso, Spec("Blunt", 10)));
            Assert.That(damage.GetAllDamage(head).GetTotal(), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(damage.GetAllDamage(torso).GetTotal(), Is.EqualTo(FixedPoint2.New(10)));
        });
    }

    [Test]
    public async Task EmptyCoverageAndSymmetryTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var allArmor = entityManager.SpawnEntity("WoundFoundationArmorAll", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var inventory = entityManager.System<InventorySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;
            var leftArm = parts.Single(part => part.Component.PartType == BodyPartType.Arm &&
                                               part.Component.Symmetry == BodyPartSymmetry.Left).Id;
            var rightArm = parts.Single(part => part.Component.PartType == BodyPartType.Arm &&
                                                part.Component.Symmetry == BodyPartSymmetry.Right).Id;

            Assert.That(inventory.TryEquip(body, allArmor, "head"), Is.True);
            Assert.That(routing.TryApplyPartDamage(body, torso, Spec("Blunt", 10)));
            Assert.That(damage.GetAllDamage(torso).GetTotal(), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(inventory.TryUnequip(body, "head"), Is.True);

            var symmetryArmor = entityManager.SpawnEntity("WoundFoundationArmorLeftArm", map.GridCoords);
            Assert.That(inventory.TryEquip(body, symmetryArmor, "outerClothing"), Is.True);
            Assert.That(routing.TryApplyPartDamage(body, leftArm, Spec("Blunt", 10)));
            Assert.That(routing.TryApplyPartDamage(body, rightArm, Spec("Blunt", 10)));
            Assert.That(damage.GetAllDamage(leftArm).GetTotal(), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(damage.GetAllDamage(rightArm).GetTotal(), Is.EqualTo(FixedPoint2.New(10)));
        });
    }

    [Test]
    public async Task LocationalModifierOverridesAndFallbackTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var armor = entityManager.SpawnEntity("WoundFoundationArmorLocational", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var inventory = entityManager.System<InventorySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;
            var leftArm = parts.Single(part => part.Component.PartType == BodyPartType.Arm &&
                                               part.Component.Symmetry == BodyPartSymmetry.Left).Id;
            var rightArm = parts.Single(part => part.Component.PartType == BodyPartType.Arm &&
                                                part.Component.Symmetry == BodyPartSymmetry.Right).Id;

            Assert.That(inventory.TryEquip(body, armor, "outerClothing"), Is.True);
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 20)));
            Assert.That(routing.TryApplyPartDamage(body, torso, Spec("Blunt", 20)));
            Assert.That(routing.TryApplyPartDamage(body, leftArm, Spec("Blunt", 20)));
            Assert.That(routing.TryApplyPartDamage(body, rightArm, Spec("Blunt", 20)));

            Assert.That(damage.GetAllDamage(head).GetTotal(), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(damage.GetAllDamage(torso).GetTotal(), Is.EqualTo(FixedPoint2.New(16)));
            Assert.That(damage.GetAllDamage(leftArm).GetTotal(), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(damage.GetAllDamage(rightArm).GetTotal(), Is.EqualTo(FixedPoint2.New(15)));
        });
    }

    [Test]
    public async Task NonWoundHostUsesVanillaArmorTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFoundationVanillaBody", map.GridCoords);
            var armor = entityManager.SpawnEntity("WoundFoundationArmorAll", map.GridCoords);
            var inventory = entityManager.System<InventorySystem>();
            var damage = entityManager.System<DamageableSystem>();

            Assert.That(inventory.TryEquip(body, armor, "head"), Is.True);
            Assert.That(damage.TryChangeDamage(body, Spec("Blunt", 10)));
            Assert.That(damage.GetAllDamage(body).GetTotal(), Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    [Test]
    public async Task PainApiAndProjectionTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var pain = entityManager.System<PainSystem>();
            var effects = entityManager.System<SharedEntityEffectsSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var leftArm = parts.Single(part => part.Component.PartType == BodyPartType.Arm &&
                                               part.Component.Symmetry == BodyPartSymmetry.Left).Id;

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 10)));
            Assert.That(pain.GetPain(head), Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(pain.GetPain(body), Is.EqualTo(FixedPoint2.New(15)));

            Assert.That(routing.TryApplyPartDamage(body, leftArm, Spec("Heat", 10)));
            Assert.That(damage.GetAllDamage(leftArm).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(entityManager.GetComponent<SystemicDamageComponent>(body).Damage.GetTotal(),
                Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(pain.GetPain(leftArm), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(pain.GetPain(body), Is.EqualTo(FixedPoint2.New(15)));

            var healingBody = entityManager.SpawnEntity("WoundFoundationBody", map.GridCoords);
            var healingHead = graph.GetBodyChildren(healingBody)
                .Single(part => part.Component.PartType == BodyPartType.Head).Id;
            Assert.That(routing.TryApplyPartDamage(healingBody, healingHead, Spec("Blunt", 10)));
            Assert.That(routing.TryApplyPartDamage(healingBody, healingHead, Spec("Blunt", -10)));
            Assert.That(damage.GetAllDamage(healingHead).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(pain.GetRawPain(healingHead), Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(pain.GetRawPain(healingBody), Is.EqualTo(FixedPoint2.New(3)));

            var suppressant = new SuppressPain
            {
                Amount = 2,
                DecayDuration = TimeSpan.FromSeconds(10),
            };
            effects.ApplyEffect(body, suppressant);
            Assert.That(pain.GetPain(body), Is.EqualTo(FixedPoint2.New(13)));
            Assert.That(pain.GetRawPain(body), Is.EqualTo(FixedPoint2.New(15)));
            effects.ApplyEffect(body, suppressant);
            Assert.That(pain.GetPain(body), Is.EqualTo(FixedPoint2.New(11)));
            Assert.That(entityManager.GetComponent<PainComponent>(body).Suppression, Is.EqualTo(FixedPoint2.New(4)));

            effects.ApplyEffect(body, new SuppressPain
            {
                Amount = 1,
                DecayDuration = TimeSpan.FromSeconds(10),
                Identifier = "SecondSuppressant",
            });
            Assert.That(pain.GetPain(body), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(pain.DecayPainSuppression(body, 1f));
            Assert.That(entityManager.GetComponent<PainComponent>(body).Suppression, Is.EqualTo(FixedPoint2.New(4.5)));
            Assert.That(pain.GetPain(body), Is.EqualTo(FixedPoint2.New(10.5)));

            Assert.That(pain.RecoverPain(head, 1f));
            Assert.That(pain.GetRawPain(head), Is.EqualTo(FixedPoint2.New(14.92)));
            Assert.That(pain.GetRawPain(body), Is.EqualTo(FixedPoint2.New(14.92)));

            Assert.That(graph.TryDetachPart(head));
            Assert.That(pain.GetPain(body), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(damage.TryChangeDamage(head, Spec("Blunt", 5)));
            Assert.That(pain.GetPain(head), Is.EqualTo(FixedPoint2.New(22.42)));

            Assert.That(pain.SetPain(head, FixedPoint2.New(-1)), Is.True);
            Assert.That(pain.GetPain(head), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(pain.ChangePain(head, FixedPoint2.New(2)), Is.True);
            Assert.That(pain.GetPain(head), Is.EqualTo(FixedPoint2.New(2)));
            Assert.That(pain.ChangePain(head, FixedPoint2.New(-3)), Is.True);
            Assert.That(pain.GetPain(head), Is.EqualTo(FixedPoint2.Zero));

            entityManager.EventBus.RaiseLocalEvent(body, new RejuvenateEvent());
            Assert.That(pain.GetPain(body), Is.EqualTo(FixedPoint2.Zero));
        });
    }

    private static DamageSpecifier Spec(string type, int amount)
    {
        return new DamageSpecifier
        {
            DamageDict = { [new ProtoId<DamageTypePrototype>(type)] = FixedPoint2.New(amount) },
        };
    }
}
#pragma warning restore CS0618
