using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared._Onyx.Medical.Surgery;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Medical;

public sealed class SurgeryStepSequencePrototypeTest : GameTest
{
    private static readonly EntProtoId[] CyberneticAttachmentSurgeries =
    [
        "SurgeryAttachLeftArm",
        "SurgeryAttachRightArm",
        "SurgeryAttachLeftLeg",
        "SurgeryAttachRightLeg",
        "SurgeryAttachLeftHand",
        "SurgeryAttachRightHand",
        "SurgeryAttachLeftFoot",
        "SurgeryAttachRightFoot",
    ];

    [SidedDependency(Side.Server)] private IComponentFactory _componentFactory = default!;
    [SidedDependency(Side.Server)] private IPrototypeManager _prototypes = default!;

    [Test]
    [RunOnSide(Side.Server)]
    public void OrdinarySurgeryHasNamedFallbackSection()
    {
        var surgery = GetSurgery("SurgeryOpenIncision");
        var steps = surgery.Steps;

        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps.TryGetValue("default", out var section), Is.True);
        Assert.That(section!.Required, Is.Empty);
        Assert.That(section.Steps,
            Is.EqualTo(new EntProtoId[] { "SurgeryStepOpenIncisionScalpel", "SurgeryStepRetractSkin" }));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void EverySurgeryHasOneFallbackAndNonEmptySections()
    {
        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.Abstract ||
                !prototype.TryComp(out SurgeryComponent surgery, _componentFactory))
                continue;

            var steps = surgery.Steps;
            Assert.That(steps.Values.Count(section => section.Required.Count == 0), Is.EqualTo(1),
                $"{prototype.ID} must have exactly one unconditional step section.");
            Assert.That(steps.Values.All(section => section.Steps.Count > 0), Is.True,
                $"{prototype.ID} contains an empty step section.");
        }
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void CyberneticAttachmentsHaveFallbackAndConditionalSequences()
    {
        foreach (var id in CyberneticAttachmentSurgeries)
        {
            var surgery = GetSurgery(id);
            var steps = surgery.Steps;
            Assert.That(steps, Has.Count.EqualTo(2), id.Id);
            Assert.That(steps.TryGetValue("organic", out var organic), Is.True, id.Id);
            Assert.That(organic!.Required, Is.Empty, id.Id);
            Assert.That(steps.TryGetValue("cybernetic", out var cybernetic), Is.True, id.Id);
            Assert.That(cybernetic!.Required.Values.Any(component =>
                component.Component.GetType() == typeof(CyberneticsComponent)), Is.True, id.Id);

            foreach (var step in steps.Values.SelectMany(section => section.Steps))
            {
                var prototype = _prototypes.Index<EntityPrototype>(step);
                Assert.That(prototype.HasComp<SurgeryStepComponent>(_componentFactory), Is.True,
                    $"{id} references non-surgery step {step}.");
            }
        }
    }

    private SurgeryComponent GetSurgery(EntProtoId id)
    {
        var prototype = _prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp(out SurgeryComponent surgery, _componentFactory), Is.True, id.Id);
        return surgery!;
    }
}
