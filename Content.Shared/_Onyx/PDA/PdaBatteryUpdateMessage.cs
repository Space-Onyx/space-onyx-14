// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.PDA;

/// <summary>
/// Lightweight push for the open PDA UI. Only carries the charge values
/// and must not trigger a full UI refresh on the client.
/// </summary>
[Serializable, NetSerializable]
public sealed class PdaBatteryUpdateMessage : BoundUserInterfaceMessage
{
    public float Charge;
    public float Max;

    public PdaBatteryUpdateMessage() { }

    public PdaBatteryUpdateMessage(float charge, float max)
    {
        Charge = charge;
        Max = max;
    }
}
