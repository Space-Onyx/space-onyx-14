// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 IrisTheAmped <iristheamped@gmail.com>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.CollectiveMind;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CollectiveMindComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public ProtoId<CollectiveMindPrototype>? DefaultChannel;

    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<CollectiveMindPrototype>> Channels = new();

    [DataField, AutoNetworkedField]
    public bool HearAll;

    [DataField, AutoNetworkedField]
    public bool SeeAllNames;

    [DataField, AutoNetworkedField]
    public bool RespectAccents;

    [DataField, AutoNetworkedField]
    public bool CanUseInCrit;
}
