using Content.Shared._CorvaxGoob.Photo;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Audio.Sources;
using System.Numerics;

namespace Content.Client._CorvaxGoob.Photo.UI;

public sealed partial class PhotoCameraBoundUserInterface : BoundUserInterface
{
    private const float ControlSoundTail = 0.2f; // <Onyx-PhotoCamera>

    private readonly EyeSystem _eyeSystem;
    private readonly PhotoSystem _photoSystem;
    private readonly TransformSystem _transform;

    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IAudioManager _audioManager = default!;

    private PhotoCameraWindow? _window;
    private EntityUid? _cameraEntity;
    private Vector2 _zoomPos = Vector2.Zero;
    private float _zoomValue = 1f;
    private IAudioSource? _controlSound;
    private float _controlSoundRemaining; // <Onyx-PhotoCamera>
    private bool _capturePending;
    private bool _disposed; // <Onyx-PhotoCamera>

    public PhotoCameraBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _eyeSystem = EntMan.System<EyeSystem>();
        _photoSystem = EntMan.System<PhotoSystem>();
        _transform = EntMan.System<TransformSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PhotoCameraWindow>();
        _window.OnTakeImageAttempt += AttemptTakeImage;
        _window.OnClose += Close; // <Onyx-PhotoCamera>

        if (!_cache.TryGetResource("/Audio/_CorvaxGoob/Effects/servo_effect.ogg", out AudioResource? resource))
            return;

        _controlSound = _audioManager.CreateAudioSource(resource);
        if (_controlSound == null)
            return;

        _controlSound.Global = true;
        _controlSound.Looping = true;
        _controlSound.Volume = 2f; // <Onyx-PhotoCamera-edited>
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not PhotoCameraUiState cast)
            return;

        _cameraEntity = EntMan.GetEntity(cast.CameraEntity);
        if (EntMan.TryGetComponent<PhotoCameraComponent>(_cameraEntity, out var component))
        {
            _photoSystem.OpenCameraUi(_cameraEntity.Value, this); // <Onyx-PhotoCamera-edited>
            UpdateControl(component, 1);
        }

        if (EntMan.TryGetComponent<EyeComponent>(_cameraEntity, out var eye))
            _window.UpdateState(eye.Eye, cast.HasPaper);
    }

    protected override void Dispose(bool disposing)
    {
        // <Onyx-PhotoCamera-edited>
        _disposed = true;
        _photoSystem.CloseCameraUi(this);
        _cameraEntity = null;
        _controlSound?.Dispose();
        _controlSound = null;
        if (_window != null)
        {
            _window.OnTakeImageAttempt -= AttemptTakeImage;
            _window.OnClose -= Close;
            _window.OnDispose();
            _window = null;
        }

        base.Dispose(disposing);
        // </Onyx-PhotoCamera-edited>
    }

    public void UpdateControl(PhotoCameraComponent component, float frameTime)
    {
        if (_cameraEntity == null || _window == null)
            return;

        var pos = _zoomPos + _window.MoveInput * _zoomValue * frameTime;
        var zoom = Math.Clamp(_zoomValue + _window.ZoomInput * frameTime * (component.MaxZoom - component.MinZoom),
            component.MinZoom,
            component.MaxZoom);
        var zoomRatio = (zoom - component.MinZoom) / (component.MaxZoom - component.MinZoom);
        pos.X = Math.Clamp(pos.X, -component.ViewBox.X * 0.5f * (1 - zoomRatio), component.ViewBox.X * 0.5f * (1 - zoomRatio));
        pos.Y = Math.Clamp(pos.Y, -component.ViewBox.Y * 0.5f * (1 - zoomRatio), component.ViewBox.Y * 0.5f * (1 - zoomRatio));

        var angle = _transform.GetWorldRotation(_cameraEntity.Value);
        var grid = _transform.GetGrid(_cameraEntity.Value);
        Angle localAngle = 0;
        if (grid != null)
            localAngle = angle - _transform.GetWorldRotation(grid.Value);

        var delta = new Vector3(_zoomPos - pos, _zoomValue - zoom);
        _zoomPos = pos;
        _zoomValue = zoom;
        _window.ZoomInput = 0;

        var rotateAngle = angle.Opposite() - (localAngle - localAngle.RoundToCardinalAngle());
        _eyeSystem.SetOffset(_cameraEntity.Value, rotateAngle.RotateVec(pos));
        _eyeSystem.SetZoom(_cameraEntity.Value, new Vector2(zoom));
        _eyeSystem.SetRotation(_cameraEntity.Value, -rotateAngle);

        if (_controlSound == null)
            return;

        // <Onyx-PhotoCamera-edited>
        if (delta != Vector3.Zero)
        {
            _controlSoundRemaining = ControlSoundTail;
            _controlSound.StartPlaying();
        }
        else if ((_controlSoundRemaining -= frameTime) <= 0f)
        {
            _controlSoundRemaining = 0f;
            _controlSound.StopPlaying();
        }
        // </Onyx-PhotoCamera-edited>
    }

    private void AttemptTakeImage()
    {
        if (_window == null || _capturePending)
            return;

        _capturePending = true;
        _window.RenderImage(bytes =>
        {
            // <Onyx-PhotoCamera-edited>
            _capturePending = false;
            if (!_disposed && bytes != null)
                SendMessage(new PhotoCameraTakeImageMessage(bytes));
            // </Onyx-PhotoCamera-edited>
        });
    }
}
