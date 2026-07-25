using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.EntityEffects.Effects.Transform;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Robust.Shared.Enums;

namespace Content.IntegrationTests.Tests._Onyx.EntityEffects;

[TestOf(typeof(SexChangeEntityEffectSystem))]
public sealed class SexChangeTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly HumanoidProfileSystem _humanoid = null!;

    [Test]
    [RunOnSide(Side.Server)]
    public void ToggleAndExplicitSexChange()
    {
        var target = SSpawn("MobHuman");
        var profile = SComp<HumanoidProfileComponent>(target);
        var effects = SEntMan.System<SharedEntityEffectsSystem>();
        Assert.That(_humanoid.SetSex((target, profile), Sex.Female, true), Is.True);

        effects.ApplyEffect(target, new SexChange());
        Assert.Multiple(() =>
        {
            Assert.That(profile.Sex, Is.EqualTo(Sex.Male));
            Assert.That(profile.Gender, Is.EqualTo(Gender.Male));
        });

        effects.ApplyEffect(target, new SexChange { NewSex = Sex.Female });
        Assert.Multiple(() =>
        {
            Assert.That(profile.Sex, Is.EqualTo(Sex.Female));
            Assert.That(profile.Gender, Is.EqualTo(Gender.Male));
        });
    }
}
