using System;
using Content.Shared._Onyx.Ghost;
using NUnit.Framework;
using Robust.Shared.Timing;

namespace Content.Tests.Shared._Onyx.Ghost;

[TestFixture]
public sealed class GhostReturnToLobbyLogicTest
{
    [Test]
    public void RemainingUsesTheSameClockAsAvailability()
    {
        var currentTime = TimeSpan.FromMinutes(385);
        var availableAt = GhostReturnToLobbyLogic.ComputeAvailableAt(currentTime, 300);

        Assert.That(GhostReturnToLobbyLogic.GetRemaining(currentTime, availableAt), Is.EqualTo(TimeSpan.FromSeconds(300)));
    }
}
