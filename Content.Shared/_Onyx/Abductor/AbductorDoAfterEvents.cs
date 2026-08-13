// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Abductor;

[Serializable, NetSerializable] public sealed partial class AbductorReturnDoAfterEvent : SimpleDoAfterEvent;
[Serializable, NetSerializable] public sealed partial class AbductorGizmoMarkDoAfterEvent : SimpleDoAfterEvent;

[RegisterComponent]
public sealed partial class CuffsOnHitComponent : Component
{
    [DataField("proto")] public EntProtoId? HandcuffPrototype;
    [DataField] public TimeSpan Duration = TimeSpan.FromSeconds(1);
}

[Serializable, NetSerializable] public sealed partial class CuffsOnHitDoAfter : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class AbductorSendYourselfDoAfterEvent : SimpleDoAfterEvent
{
    [DataField(required: true)] public NetCoordinates TargetCoordinates;
    private AbductorSendYourselfDoAfterEvent() { }
    public AbductorSendYourselfDoAfterEvent(NetCoordinates coordinates) => TargetCoordinates = coordinates;
    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class AbductorAttractDoAfterEvent : SimpleDoAfterEvent
{
    [DataField(required: true)] public NetCoordinates TargetCoordinates;
    [DataField(required: true)] public NetEntity Victim;
    private AbductorAttractDoAfterEvent() { }
    public AbductorAttractDoAfterEvent(NetCoordinates coordinates, NetEntity victim) => (TargetCoordinates, Victim) = (coordinates, victim);
    public override DoAfterEvent Clone() => this;
}
