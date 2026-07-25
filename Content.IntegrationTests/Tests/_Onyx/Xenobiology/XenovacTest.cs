using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.Xenobiology.Equipment.Components;
using Content.Shared.Timing;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

[TestOf(typeof(XenovacComponent))]
public sealed class XenovacTest : GameTest
{
    [Test]
    [RunOnSide(Side.Server)]
    public void XenovacPrototypesHaveCapacityWhitelistAndDelays()
    {
        var tank = SEntMan.SpawnEntity("ClothingBackpackXenoBioTank", MapCoordinates.Nullspace);
        var nozzle = SEntMan.SpawnEntity("WeaponXenoVacNozzle", MapCoordinates.Nullspace);
        var tankComp = SEntMan.GetComponent<XenovacTankComponent>(tank);
        var nozzleComp = SEntMan.GetComponent<XenovacComponent>(nozzle);
        var delays = SEntMan.GetComponent<UseDelayComponent>(nozzle);

        Assert.Multiple(() =>
        {
            Assert.That(tankComp.Capacity, Is.EqualTo(5));
            Assert.That(tankComp.ContainerId, Is.EqualTo("xenovac-storage"));
            Assert.That(nozzleComp.Whitelist.Tags, Does.Contain("XenoSlime"));
            Assert.That(nozzleComp.Whitelist.Components, Does.Contain("Skinnable"));
            Assert.That(delays.Delays["suction"].Length, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(delays.Delays["release"].Length, Is.EqualTo(TimeSpan.FromSeconds(3)));
        });
    }
}
