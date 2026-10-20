// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Client._Onyx.IconSmoothing;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests._Onyx.IconSmoothing;

[TestFixture]
public sealed class CardinalFlagsSpriteSmoothingTest
{
    [Test]
    public void LegacyDirectionFlags()
    {
        var smoothing = new CardinalFlagsSpriteSmoothing
        {
            Base = "weed_dir",
            Mask = new HashSet<string> { "weeds" },
        };

        for (var flags = 0; flags < 16; flags++)
        {
            var layers = new HashSet<string>[8];
            layers[0] = new HashSet<string> { (flags & 2) != 0 ? "weeds" : "other" };
            layers[2] = new HashSet<string> { (flags & 4) != 0 ? "weeds" : "other" };
            layers[4] = new HashSet<string> { (flags & 1) != 0 ? "weeds" : "other" };
            layers[6] = new HashSet<string> { (flags & 8) != 0 ? "weeds" : "other" };
            layers[1] = layers[3] = layers[5] = layers[7] = new HashSet<string> { "weeds" };

            var state = smoothing.EnumerateStates(layers, default, null!).Single();
            Assert.That(state.State, Is.EqualTo("weed_dir" + flags));
        }
    }
}
