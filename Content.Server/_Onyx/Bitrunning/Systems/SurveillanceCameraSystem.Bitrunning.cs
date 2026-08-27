using Content.Server._Onyx.Bitrunning.Components;
using Content.Shared.SurveillanceCamera.Components;

namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraSystem
{
    public void ConfigureBitrunningCamera(EntityUid uid, SurveillanceCameraComponent camera, string avatarName)
    {
        camera.NetworkSet = true;
        camera.NameSet = true;
        camera.UseEntityNameAsCameraId = false;
        camera.CameraId = avatarName;
        Dirty(uid, camera);
    }

    public EntityUid ResolveBitrunningCameraTarget(EntityUid camera)
    {
        return TryComp<AvatarNavRelayComponent>(camera, out var relay) && relay.RelayEntity is { } avatar
            ? avatar
            : camera;
    }
}
