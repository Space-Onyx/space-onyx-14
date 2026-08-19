using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Wounds;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

[TestFixture]
public sealed class WoundSurgeryScarTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WoundSurgeryPart
  components:
  - type: BodyPart
    partType: Chest
  - type: Woundable

- type: entity
  id: WoundSurgeryCloseEffect
  components:
  - type: SurgeryCloseIncisionEffect
";

    [Test]
    public async Task ChanceAndRepeatCloseTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var map = await Pair.CreateTestMap();

        try
        {
            await server.WaitAssertion(() =>
            {
                var wounds = entities.System<WoundSystem>();
                var effect = entities.SpawnEntity("WoundSurgeryCloseEffect", map.GridCoords);

                configuration.SetCVar(CCVars.SurgeryScarChance, 0f);
                var noScarPart = entities.SpawnEntity("WoundSurgeryPart", map.GridCoords);
                var noScar = wounds.CreateOrMergeWound(noScarPart, new ProtoId<WoundPrototype>("SurgicalIncisionWound"), 10)!.Value;
                Raise(effect, noScarPart, entities);
                Assert.That(entities.GetComponent<WoundComponent>(noScar).State, Is.EqualTo(WoundState.Closed));
                Assert.That(entities.GetComponent<WoundBleedingComponent>(noScar).Treatment, Is.EqualTo(BleedingTreatment.Cauterized));
                Assert.That(ScarCount(noScarPart, wounds, entities), Is.Zero);

                configuration.SetCVar(CCVars.SurgeryScarChance, 1f);
                var scarPart = entities.SpawnEntity("WoundSurgeryPart", map.GridCoords);
                wounds.CreateOrMergeWound(scarPart, new ProtoId<WoundPrototype>("SurgicalIncisionWound"), 10);
                Raise(effect, scarPart, entities);
                Assert.That(ScarCount(scarPart, wounds, entities), Is.EqualTo(1));
                Raise(effect, scarPart, entities);
                Assert.That(ScarCount(scarPart, wounds, entities), Is.EqualTo(1));
            });
        }
        finally
        {
            await server.WaitPost(() => configuration.SetCVar(CCVars.SurgeryScarChance,
                CCVars.SurgeryScarChance.DefaultValue));
        }
    }

    private static void Raise(EntityUid effect, EntityUid part, IEntityManager entities)
    {
        var ev = new SurgeryStepEvent(EntityUid.Invalid, EntityUid.Invalid, part, []);
        entities.EventBus.RaiseLocalEvent(effect, ref ev);
    }

    private static int ScarCount(EntityUid part, WoundSystem wounds, IEntityManager entities)
    {
        return wounds.GetWounds((part, entities.GetComponent<WoundableComponent>(part)))
            .Count(wound => entities.HasComponent<WoundScarComponent>(wound));
    }
}
