// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    private void PruneJobAlternatives()
    {
        foreach (var jobId in _jobAlternatives.Keys.Where(key => !_jobPriorities.ContainsKey(key)).ToList())
        {
            _jobAlternatives.Remove(jobId);
        }
    }
}
