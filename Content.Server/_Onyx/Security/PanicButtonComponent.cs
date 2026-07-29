// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Security;

[RegisterComponent]
public sealed partial class PanicButtonComponent : Component
{
    [DataField]
    public LocId DistressMessage = "panic-button-distress";

    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Security";
}
