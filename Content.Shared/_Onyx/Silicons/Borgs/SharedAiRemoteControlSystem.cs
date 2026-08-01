using Content.Shared._Onyx.Silicons.Borgs.Components;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Silicons.Borgs;

public abstract partial class SharedAiRemoteControlSystem : EntitySystem
{
    [Dependency] private SharedStationAiSystem _stationAi = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public void ReturnMindIntoAi(Entity<AiRemoteControllerComponent> entity)
    {
        if (entity.Comp.AiHolder is not { } holder ||
            entity.Comp.LinkedMind is not { } mind ||
            !_stationAi.TryGetCore(holder, out var core) ||
            core.Comp?.RemoteEntity is not { } remote)
            return;

        _mind.TransferTo(mind, holder);
        _stationAi.SwitchRemoteEntityMode(core, true);
        entity.Comp.AiHolder = null;
        entity.Comp.LinkedMind = null;
        _transform.SetCoordinates(remote, Transform(entity).Coordinates);
    }
}

public sealed partial class ReturnMindIntoAiEvent : InstantActionEvent;
public sealed partial class ToggleRemoteDevicesScreenEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum RemoteDeviceUiKey : byte
{
    Key
}
