using Content.Client._CorvaxGoob.Photo.UI;
using Content.Shared._CorvaxGoob.Photo;

namespace Content.Client._CorvaxGoob.Photo;

public sealed partial class PhotoSystem : SharedPhotoSystem
{
    private readonly Dictionary<PhotoCameraBoundUserInterface, PhotoCameraComponent> _activeCameras = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (window, component) in _activeCameras)
            window.UpdateControl(component, frameTime);
    }

    public void OpenCameraUi(PhotoCameraComponent component, PhotoCameraBoundUserInterface window)
    {
        _activeCameras[window] = component;
    }

    public void CloseCameraUi(PhotoCameraBoundUserInterface window)
    {
        _activeCameras.Remove(window);
    }
}
