// SPDX-FileCopyrightText: 2025 BombasterDS <deniskaporoshok@gmail.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Footprints;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FootprintComponent : Component
{
    [AutoNetworkedField, ViewVariables]
    public List<Footprint> Footprints = new();
}

[Serializable, NetSerializable]
public readonly record struct Footprint(Vector2 Offset, Angle Rotation, Color Color, string State);
