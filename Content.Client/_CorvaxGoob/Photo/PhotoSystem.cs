using Content.Client._CorvaxGoob.Photo.UI;
using Content.Shared._CorvaxGoob.Photo;

namespace Content.Client._CorvaxGoob.Photo;

public sealed partial class PhotoSystem : SharedPhotoSystem
{
    // <Onyx-PhotoCamera-edited>
    private readonly Dictionary<PhotoCameraBoundUserInterface, EntityUid> _activeCameras = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (window, camera) in _activeCameras)
        {
            if (TryComp<PhotoCameraComponent>(camera, out var component))
                window.UpdateControl(component, frameTime);
        }
    }

    public void OpenCameraUi(EntityUid camera, PhotoCameraBoundUserInterface window)
    {
        _activeCameras[window] = camera;
    }
    // </Onyx-PhotoCamera-edited>

    public void CloseCameraUi(PhotoCameraBoundUserInterface window)
    {
        _activeCameras.Remove(window);
    }
}
