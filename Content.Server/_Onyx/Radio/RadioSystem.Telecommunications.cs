// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Onyx.Telecommunications;
using Content.Shared.Radio;

namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    [Dependency] private TelecommunicationsChainSystem _telecommunications = default!;

    private (bool CanBroadcast, string Message) RouteTelecommunications(
        bool canSend,
        EntityUid radioSource,
        RadioChannelPrototype channel,
        EntityUid messageSource,
        string message)
    {
        if (!canSend || channel.LongRange || _exemptQuery.HasComp(radioSource))
            return (canSend, message);

        var route = _telecommunications.RouteSignal(radioSource, channel, messageSource, message);
        return (route.CanBroadcast, route.Message);
    }

    private void CompleteTelecommunications(EntityUid radioSource)
    {
        _telecommunications.CompleteTransmission(radioSource);
    }

}
