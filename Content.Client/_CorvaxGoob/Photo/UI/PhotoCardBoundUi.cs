using Content.Shared._CorvaxGoob.Photo;
using Robust.Client.UserInterface;

namespace Content.Client._CorvaxGoob.Photo.UI;

public sealed class PhotoCardBoundUserInterface : BoundUserInterface
{
    private PhotoCardWindow? _window;

    public PhotoCardBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PhotoCardWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is PhotoCardUiState { ImageData: not null } cast)
            _window.ShowImage(cast.ImageData);
    }
}
