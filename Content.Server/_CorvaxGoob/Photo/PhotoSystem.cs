using Content.Server.Hands.Systems;
using Content.Server.Materials;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Popups;
using Content.Shared._CorvaxGoob.CCCVars;
using Content.Shared._CorvaxGoob.Photo;
using Content.Shared.Materials;
using Content.Shared.Timing;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using System.Buffers.Binary;

namespace Content.Server._CorvaxGoob.Photo;

public sealed partial class PhotoSystem : SharedPhotoSystem
{
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private MaterialStorageSystem _material = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UseDelaySystem _delay = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;

    private const int MaxImageSize = 1024 * 96;
    private const int ImageWidth = 250;
    private const int ImageHeight = 250;
    private bool _photoTimeRequiredEnabled = true;
    private float _photoTimeRequiredHours = 20;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhotoCameraComponent, AfterActivatableUIOpenEvent>(OnOpenCameraInterface);
        Subs.BuiEvents<PhotoCameraComponent>(PhotoCameraUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnCameraBoundUiClose);
            subs.Event<PhotoCameraTakeImageMessage>(OnTakeImageMessage);
        });
        SubscribeLocalEvent<PhotoCameraComponent, MaterialAmountChangedEvent>(OnPaperInserted);
        SubscribeLocalEvent<PhotoCardComponent, AfterActivatableUIOpenEvent>(OnOpenCardInterface);

        Subs.CVar(_cfg, CCCVars.PhotoPlayTimeRequire, value => _photoTimeRequiredEnabled = value, true);
        Subs.CVar(_cfg, CCCVars.PhotoPlayTimeHours, value => _photoTimeRequiredHours = value, true);
    }

    private void OnOpenCameraInterface(EntityUid uid, PhotoCameraComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateCameraInterface(uid, component);
        EnsureComp<PhotoCameraUserComponent>(args.User);
    }

    private void OnCameraBoundUiClose(EntityUid uid, PhotoCameraComponent component, BoundUIClosedEvent args)
    {
        RemComp<PhotoCameraUserComponent>(args.Actor);
    }

    private void OnTakeImageMessage(EntityUid uid, PhotoCameraComponent component, PhotoCameraTakeImageMessage message)
    {
        if (!_userInterface.IsUiOpen(uid, PhotoCameraUiKey.Key, message.Actor) ||
            message.Data.Length > MaxImageSize ||
            !IsValidPhotoPng(message.Data))
            return;

        TryTakeImage(uid, component, message.Actor, message.Data);
    }

    private void UpdateCameraInterface(EntityUid uid, PhotoCameraComponent component)
    {
        var hasPaper = _material.CanChangeMaterialAmount(uid, component.CardMaterial, -component.CardCost);
        _userInterface.SetUiState(uid, PhotoCameraUiKey.Key, new PhotoCameraUiState(GetNetEntity(uid), hasPaper));
    }

    private void OnPaperInserted(EntityUid uid, PhotoCameraComponent component, MaterialAmountChangedEvent args)
    {
        if (TryComp<MaterialStorageComponent>(uid, out var storage))
            Dirty(uid, storage);

        if (_userInterface.IsUiOpen(uid, PhotoCameraUiKey.Key))
            UpdateCameraInterface(uid, component);
    }

    private bool TryTakeImage(EntityUid uid, PhotoCameraComponent component, EntityUid actor, byte[] imageData)
    {
        if (_delay.IsDelayed(uid))
            return false;

        if (!_material.CanChangeMaterialAmount(uid, component.CardMaterial, -component.CardCost))
        {
            _audio.PlayPvs(component.ErrorSound, uid);
            _popup.PopupEntity(Loc.GetString("photo-camera-no-paper"), uid, actor);
            return false;
        }

        if (_photoTimeRequiredEnabled)
        {
            if (!_player.TryGetSessionByEntity(actor, out var session))
                return false;

            if (_playTime.GetOverallPlaytime(session).TotalHours < _photoTimeRequiredHours)
            {
                _audio.PlayPvs(component.ErrorSound, uid);
                _popup.PopupEntity(Loc.GetString("photo-camera-not-enough-playtime"), actor, session);
                return false;
            }
        }

        _delay.TryResetDelay(uid);
        var printed = PrintCard(uid, component, actor, imageData);
        _audio.PlayPvs(printed ? component.PhotoSound : component.ErrorSound, uid);
        return printed;
    }

    private bool PrintCard(EntityUid uid, PhotoCameraComponent component, EntityUid actor, byte[] imageData)
    {
        if (!_material.TryChangeMaterialAmount(uid, component.CardMaterial, -component.CardCost))
        {
            _popup.PopupEntity(Loc.GetString("photo-camera-no-paper"), uid, actor);
            return false;
        }

        var card = Spawn(component.CardPrototype, _transform.GetMapCoordinates(uid));
        if (TryComp<PhotoCardComponent>(card, out var photo))
            photo.ImageData = imageData;

        _hands.TryPickupAnyHand(actor, card);

        UpdateCameraInterface(uid, component);
        return true;
    }

    private static bool IsValidPhotoPng(ReadOnlySpan<byte> data)
    {
        return data.Length >= 24 &&
               data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
               data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A &&
               BinaryPrimitives.ReadUInt32BigEndian(data[8..12]) == 13 &&
               data[12] == 'I' && data[13] == 'H' && data[14] == 'D' && data[15] == 'R' &&
               BinaryPrimitives.ReadUInt32BigEndian(data[16..20]) == ImageWidth &&
               BinaryPrimitives.ReadUInt32BigEndian(data[20..24]) == ImageHeight;
    }

    private void OnOpenCardInterface(EntityUid uid, PhotoCardComponent component, AfterActivatableUIOpenEvent args)
    {
        _userInterface.SetUiState(uid, PhotoCardUiKey.Key, new PhotoCardUiState(component.ImageData));
    }
}
