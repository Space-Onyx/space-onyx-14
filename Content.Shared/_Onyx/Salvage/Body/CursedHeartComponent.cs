using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Salvage.Body;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CursedHeartComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? PumpActionEntity;

    public TimeSpan LastPump;

    [DataField]
    public float MaxDelay = 5f;
}

[RegisterComponent]
public sealed partial class CursedHeartGrantComponent : Component;

public sealed partial class PumpHeartActionEvent : InstantActionEvent;
