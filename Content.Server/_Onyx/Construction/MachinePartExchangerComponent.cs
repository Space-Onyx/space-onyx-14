// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Audio;

namespace Content.Server._Onyx.Construction;

[RegisterComponent]
public sealed partial class MachinePartExchangerComponent : Component
{
    /// <summary>
    /// Allows distant use without opening maintenance panel.
    /// </summary>
    [DataField]
    public bool Remote;

    /// <summary>
    /// Time required to exchange parts.
    /// </summary>
    [DataField]
    public TimeSpan ExchangeTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Sound played during exchange.
    /// </summary>
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Items/rped.ogg");

    /// <summary>
    /// Optional beam used by remote exchangers.
    /// </summary>
    [DataField]
    public string? BeamPrototype;

}
