using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Body;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
[TestOf(typeof(InitialBodySystem))]
public sealed class InitialBodyValidationTest : GameTest
{
    [SidedDependency(Side.Server)] private IComponentFactory _componentFactory = default!;
    [SidedDependency(Side.Server)] private IPrototypeManager _prototype = default!;

    [Test]
    [RunOnSide(Side.Server)]
    public void InternalChildOrgansAreNotDetachable()
    {
        using var scope = Assert.EnterMultipleScope();

        foreach (var proto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || Pair.IsTestEntityPrototype(proto.ID))
                continue;

            if (!proto.HasComp<InternalChildOrganComponent>(_componentFactory))
                continue;

            if (proto.HasComp<DetachableOrganComponent>(_componentFactory))
            {
                Assert.Fail($"{proto.ID} has both {nameof(InternalChildOrganComponent)} and {nameof(DetachableOrganComponent)}. Pick a lane, make your organs internal or detachable, but not both.");
            }
        }
    }

}
