using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.NPC.FactionStatusEffects;
using Content.Shared.EntityEffects;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.NPC;

[TestOf(typeof(FactionOverrideStatusEffectSystem))]
public sealed class FactionOverrideTest : GameTest
{
    private static readonly ProtoId<NpcFactionPrototype> AllHostile = "AllHostile";
    private static readonly ProtoId<NpcFactionPrototype> Passive = "Passive";
    private static readonly ProtoId<NpcFactionPrototype> SimpleNeutral = "SimpleNeutral";

    [SidedDependency(Side.Server)] private readonly NpcFactionSystem _factions = null!;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestOnyxFactionTarget
  components:
  - type: NpcFactionMember
    factions:
    - Passive

- type: entity
  parent: StatusEffectBase
  id: TestOnyxNeutralFactionOverride
  components:
  - type: StatusEffect
    whitelist:
      components:
      - NpcFactionMember
  - type: FactionOverrideStatusEffect
    faction: SimpleNeutral
";

    [Test]
    [RunOnSide(Side.Server)]
    public void PermanentAndTemporaryFactionChanges()
    {
        var target = SSpawn("TestOnyxFactionTarget");
        var effects = SEntMan.System<SharedEntityEffectsSystem>();
        var statuses = SEntMan.System<StatusEffectsSystem>();

        effects.ApplyEffect(target, new SetFaction { Faction = SimpleNeutral });
        Assert.Multiple(() =>
        {
            Assert.That(_factions.IsMember(target, SimpleNeutral), Is.True);
            Assert.That(_factions.IsMember(target, Passive), Is.False);
        });

        Assert.That(statuses.TryAddStatusEffectDuration(target,
            "StatusEffectOnyxAllHostileFaction",
            out var status,
            TimeSpan.FromSeconds(10)), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(_factions.IsMember(target, AllHostile), Is.True);
            Assert.That(_factions.IsMember(target, SimpleNeutral), Is.False);
        });

        SEntMan.DeleteEntity(status.Value);
        Assert.Multiple(() =>
        {
            Assert.That(_factions.IsMember(target, SimpleNeutral), Is.True);
            Assert.That(_factions.IsMember(target, AllHostile), Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void OverlappingOverridesRestoreAfterLastRemoval()
    {
        var target = SSpawn("TestOnyxFactionTarget");
        var statuses = SEntMan.System<StatusEffectsSystem>();

        Assert.That(statuses.TryAddStatusEffectDuration(target,
            "StatusEffectOnyxAllHostileFaction",
            out var hostile,
            TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(statuses.TryAddStatusEffectDuration(target,
            "TestOnyxNeutralFactionOverride",
            out var neutral,
            TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(_factions.IsMember(target, SimpleNeutral), Is.True);

        SEntMan.DeleteEntity(hostile.Value);
        Assert.Multiple(() =>
        {
            Assert.That(_factions.IsMember(target, SimpleNeutral), Is.True);
            Assert.That(_factions.IsMember(target, Passive), Is.False);
        });

        SEntMan.DeleteEntity(neutral.Value);
        Assert.Multiple(() =>
        {
            Assert.That(_factions.IsMember(target, Passive), Is.True);
            Assert.That(_factions.IsMember(target, SimpleNeutral), Is.False);
        });
    }
}
