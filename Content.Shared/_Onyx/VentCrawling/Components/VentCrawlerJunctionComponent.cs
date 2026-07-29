// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Onyx.VentCrawling;

[RegisterComponent, Virtual]
public partial class VentCrawlerJunctionComponent : Component
{
    [DataField("degrees")]
    public List<Angle> Degrees = new();
}
