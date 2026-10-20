// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Prototypes;

public sealed class PartialPrototypeInheritanceTest : GameTest
{
    [SidedDependency(Side.Server)] private IPrototypeManager _prototypes = default!;

    private static readonly EntProtoId BaseWelder = "BaseWelder";
    private static readonly EntProtoId Welder = "Welder";

    [Test]
    [RunOnSide(Side.Server)]
    public void WelderPartialPreservesParentsAndInheritedComponents()
    {
        string[] expected = ["SolutionWelder", "BaseItem", "WelderRepairModes"];
        var prototype = _prototypes.Index(BaseWelder);
        Assert.That(prototype.Parents, Is.EqualTo(expected));
        Assert.That(_prototypes.EnumerateAllParents<EntityPrototype>(BaseWelder.Id)
            .Select(parent => parent.id), Is.SupersetOf(expected));

        foreach (var parentId in expected)
        {
            var parent = _prototypes.Index<EntityPrototype>(parentId);
            foreach (var component in parent.Components.Keys)
            {
                Assert.That(prototype.Components.ContainsKey(component), Is.True,
                    $"BaseWelder lost {component} inherited from {parentId}.");
            }
        }

        var welder = _prototypes.Index(Welder);
        Assert.That(welder.Components.ContainsKey("WelderRepairModes"), Is.True);
        Assert.That(welder.Components.ContainsKey("Item"), Is.True);
        Assert.That(welder.Components.ContainsKey("SolutionContainerManager"), Is.True);
    }
}
