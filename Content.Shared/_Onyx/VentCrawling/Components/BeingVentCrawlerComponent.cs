// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Onyx.VentCrawling;

[RegisterComponent]
public sealed partial class BeingVentCrawlerComponent : Component
{
    [DataField("holder")]
    private EntityUid _holder;

    public EntityUid Holder
    {
        get => _holder;
        set
        {
            if (_holder == value)
                return;

            if (value == default)
                throw new ArgumentException("Holder cannot be default EntityUid");

            _holder = value;
        }
    }
}
