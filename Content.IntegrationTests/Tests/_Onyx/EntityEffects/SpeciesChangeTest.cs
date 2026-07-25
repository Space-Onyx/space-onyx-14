using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Onyx.EntityEffects.Effects.Transform;
using Content.Server.Polymorph.Components;
using Content.Shared._Onyx.EntityEffects.Effects.Transform;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.EntityEffects;

[TestOf(typeof(PermanentSpeciesChangeSystem))]
public sealed class SpeciesChangeTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly SharedBodySystem _body = null!;
    [SidedDependency(Side.Server)] private readonly WoundSystem _wounds = null!;

    [Test]
    [RunOnSide(Side.Server)]
    public void SpeciesChangeIsPermanent()
    {
        var source = SSpawn("MobHuman");
        var sourceHead = _body.GetBodyChildrenOfType(source, BodyPartType.Head).Single().Id;
        var wound = _wounds.CreateOrMergeWound(sourceHead, "SlashWound", 10)!.Value;
        var changed = SEntMan.System<PermanentSpeciesChangeSystem>().TryChange(source, "Felinid");

        Assert.That(changed, Is.Not.Null);
        var targetHead = _body.GetBodyChildrenOfType(changed.Value, BodyPartType.Head).Single().Id;
        Assert.Multiple(() =>
        {
            Assert.That(SComp<HumanoidProfileComponent>(changed.Value).Species, Is.EqualTo("Felinid"));
            Assert.That(SEntMan.HasComponent<PolymorphedEntityComponent>(changed.Value), Is.False);
            Assert.That(SComp<WoundComponent>(wound).HoldingPart, Is.EqualTo(targetHead));
            Assert.That(SComp<WoundComponent>(wound).Severity, Is.EqualTo(FixedPoint2.New(10)));
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void RandomSpeciesHonorsBlacklistAndEmptyPool()
    {
        ProtoId<SpeciesPrototype> human = "Human";
        ProtoId<SpeciesPrototype> felinid = "Felinid";
        var effects = SEntMan.System<SharedEntityEffectsSystem>();
        var source = SSpawn("MobHuman");

        effects.ApplyEffect(source, new RandomSpeciesChange
        {
            Whitelist = [human, felinid],
            Blacklist = [human],
        });

        var foundFelinid = false;
        var query = SEntMan.EntityQueryEnumerator<HumanoidProfileComponent>();
        while (query.MoveNext(out _, out var profile))
            foundFelinid |= profile.Species == felinid;
        Assert.That(foundFelinid, Is.True);

        var unchanged = SSpawn("MobHuman");
        effects.ApplyEffect(unchanged, new RandomSpeciesChange
        {
            Whitelist = [human],
            Blacklist = [human],
        });
        Assert.That(SComp<HumanoidProfileComponent>(unchanged).Species, Is.EqualTo(human));
    }
}
