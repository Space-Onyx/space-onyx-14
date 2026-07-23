// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Construction;

[RegisterComponent, NetworkedComponent, Access(typeof(UpgradeKitSystem))]
public sealed partial class UpgradeKitComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField(required: true)]
    public EntityWhitelist Blacklist = new();

    [DataField(required: true)]
    public ComponentRegistry Components = new();

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    [DataField]
    public SoundSpecifier? UpgradeSound = new SoundPathSpecifier("/Audio/Items/rped.ogg");

    public EntityUid? SoundStream;
}

[Serializable, NetSerializable]
public sealed partial class UpgradeKitDoAfterEvent : SimpleDoAfterEvent;
