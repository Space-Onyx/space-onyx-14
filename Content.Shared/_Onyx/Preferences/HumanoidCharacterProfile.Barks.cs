// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.CCVar;
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

        var minPitch = configManager.GetCVar(ADTCCVars.BarksMinPitch);
        var maxPitch = configManager.GetCVar(ADTCCVars.BarksMaxPitch);
        var minDelay = configManager.GetCVar(ADTCCVars.BarksMinDelay);
        var maxDelay = configManager.GetCVar(ADTCCVars.BarksMaxDelay);

        var minVar = Math.Clamp(Bark.MinVar, minDelay, Bark.MaxVar);
        var maxVar = Math.Clamp(Bark.MaxVar, minVar, maxDelay);
        Bark = new BarkData(
            Bark.Proto,
            Math.Clamp(Bark.Pitch, minPitch, maxPitch),
            minVar,
            maxVar);
    }
}
