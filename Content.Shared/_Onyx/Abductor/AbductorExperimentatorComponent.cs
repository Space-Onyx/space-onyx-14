// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Abductor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedAbductorExperimentatorSystem))]
public sealed partial class AbductorExperimentatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Console;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ContainerId = "storage";
}

[Serializable, NetSerializable]
public enum AbductorExperimentatorVisuals : byte
{
    Full,
}
