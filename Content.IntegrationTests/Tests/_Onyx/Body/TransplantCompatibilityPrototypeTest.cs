using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.Body;
using Content.Shared._Onyx.Body.Prototypes;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Body;

public sealed class TransplantCompatibilityPrototypeTest : GameTest
{
    private static readonly ProtoId<TransplantCompatibilityPrototype> Mechanical = "Mechanical";
    private static readonly ProtoId<TransplantCompatibilityPrototype> Biosynthetic = "Biosynthetic";
    private static readonly ProtoId<TransplantCompatibilityPrototype> Cybernetic = "Cybernetic";
    private static readonly EntProtoId[] BasicCybernetics =
    [
        "LeftArmCybernetic",
        "RightArmCybernetic",
        "LeftHandCybernetic",
        "RightHandCybernetic",
        "LeftLegCybernetic",
        "RightLegCybernetic",
        "LeftFootCybernetic",
        "RightFootCybernetic",
    ];

    [SidedDependency(Side.Server)] private IComponentFactory _componentFactory = default!;
    [SidedDependency(Side.Server)] private IPrototypeManager _prototypes = default!;

    [Test]
    [RunOnSide(Side.Server)]
    public void EveryConcreteBodyPartAndOrganHasCompatibilityProfile()
    {
        var missing = _prototypes.EnumeratePrototypes<EntityPrototype>()
            .Where(proto => !proto.Abstract &&
                (proto.HasComp<BodyPartComponent>(_componentFactory) || proto.HasComp<DetachableOrganComponent>(_componentFactory)) &&
                !proto.HasComp<TransplantCompatibilityComponent>(_componentFactory))
            .Select(proto => proto.ID)
            .Order()
            .ToArray();

        Assert.That(missing, Is.Empty,
            $"Body parts and organs without transplant compatibility: {string.Join(", ", missing)}");
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void MechanicalRecipientsRejectBiosyntheticTransplants()
    {
        var mechanical = _prototypes.Index(Mechanical);
        var biosynthetic = _prototypes.Index(Biosynthetic);

        Assert.That(mechanical.Accepts.Overlaps(biosynthetic.Provides), Is.False,
            "Mechanical recipients such as IPC must reject biosynthetic body parts and organs.");
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void BasicCyberneticsKeepCyberneticCompatibilityAfterInheritance()
    {
        foreach (var id in BasicCybernetics)
        {
            var prototype = _prototypes.Index(id);
            Assert.That(prototype.TryGetComponent(out TransplantCompatibilityComponent compatibility, _componentFactory), Is.True,
                $"{id} has no transplant compatibility profile.");
            Assert.That(compatibility!.Profile, Is.EqualTo(Cybernetic),
                $"{id} must reject organic children even though it also inherits from an organic body part.");
        }
    }
}
