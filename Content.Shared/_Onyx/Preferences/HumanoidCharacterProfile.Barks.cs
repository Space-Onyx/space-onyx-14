// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.CCVar;
using Content.Shared._Onyx.SpeechBarks;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    private void EnsureBarkValid(IPrototypeManager prototypeManager, IConfigurationManager configManager)
    {
        if (!prototypeManager.TryIndex(Bark.Proto, out BarkPrototype? barkProto) || !barkProto.RoundStart)
        {
            Bark = new BarkData();
            return;
        }

        var minPitch = configManager.GetCVar(CCVars.BarksMinPitch);
        var maxPitch = configManager.GetCVar(CCVars.BarksMaxPitch);
        var minDelay = configManager.GetCVar(CCVars.BarksMinDelay);
        var maxDelay = configManager.GetCVar(CCVars.BarksMaxDelay);

        var minVar = Math.Clamp(Bark.MinVar, minDelay, Bark.MaxVar);
        var maxVar = Math.Clamp(Bark.MaxVar, minVar, maxDelay);
        Bark = new BarkData(
            Bark.Proto,
            Math.Clamp(Bark.Pitch, minPitch, maxPitch),
            minVar,
            maxVar);
    }
}
