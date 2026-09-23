// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.GameTicking;

public abstract partial class GameTicker
{
    public DateTime StationStartDateTime { get; protected set; }

    public DateTime StationDateTime
    {
        get
        {
            if (StationStartDateTime == default)
                return DateTime.Now;

            var elapsed = RoundDuration();
            return StationStartDateTime.Add(elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero);
        }
    }
}
