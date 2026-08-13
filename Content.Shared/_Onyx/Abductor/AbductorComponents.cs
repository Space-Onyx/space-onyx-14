// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Abductor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorHumanObservationConsoleComponent : Component
{
    [DataField(readOnly: true)] public EntProtoId? RemoteEntityProto = "AbductorHumanObservationConsoleEye";
    [DataField, AutoNetworkedField] public NetEntity? RemoteEntity;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorConsoleComponent : Component
{
    [DataField, AutoNetworkedField] public NetEntity? Target;
    [DataField, AutoNetworkedField] public NetEntity? AlienPod;
    [DataField, AutoNetworkedField] public NetEntity? Experimentator;
    [DataField, AutoNetworkedField] public NetEntity? Armor;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AbductorAlienPadComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorGizmoComponent : Component
{
    [DataField, AutoNetworkedField] public NetEntity? Target;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AbductorComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorVictimComponent : Component
{
    [DataField, AutoNetworkedField] public EntityCoordinates? Position;
    [DataField, AutoNetworkedField] public bool Implanted;
    [DataField, AutoNetworkedField] public TimeSpan? LastActivation;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AbductorOrganComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorScientistComponent : Component
{
    [DataField, AutoNetworkedField] public EntityCoordinates? SpawnPosition;
    [DataField, AutoNetworkedField] public EntityUid? Console;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RemoteEyeSourceContainerComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid? Actor;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorsAbilitiesComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid? ExitConsole;
    [DataField, AutoNetworkedField] public EntityUid? SendYourself;
    [DataField] public EntityUid[] HiddenActions = [];
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorVestComponent : Component
{
    [DataField, AutoNetworkedField] public AbductorArmorMode CurrentState = AbductorArmorMode.Stealth;
}

[RegisterComponent]
public sealed partial class AbductConditionComponent : Component
{
    [DataField] public int Abducted;
    [DataField] public HashSet<NetEntity> AbductedEntities = [];
}

public sealed partial class ExitConsoleEvent : InstantActionEvent;
public sealed partial class SendYourselfEvent : WorldTargetActionEvent;
public sealed partial class AbductorReturnToShipEvent : InstantActionEvent;
