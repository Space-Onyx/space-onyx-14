// SPDX-FileCopyrightText: 2026 Neyran
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Onyx.VentCrawling;

[RegisterComponent]
public sealed partial class VentCrawlerLayerTransitionComponent : Component
{
    [DataField]
    public byte Layers = 3;
}
