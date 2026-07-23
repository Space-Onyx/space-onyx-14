using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Examine;
using Content.Shared.Materials;
using Content.Shared.Movement.Events;

namespace Content.Shared._CorvaxGoob.Photo;

public abstract partial class SharedPhotoSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedMaterialStorageSystem _material = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhotoCameraComponent, ExaminedEvent>(OnCameraExamined);
        SubscribeLocalEvent<PhotoCameraUserComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<PhotoCameraUserComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PhotoCameraUserComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, PhotoCameraUserComponent component, ComponentStartup args)
    {
        _actionBlocker.UpdateCanMove(uid);
        _alerts.ShowAlert(uid, component.AlertPrototype);
    }

    private void OnShutdown(EntityUid uid, PhotoCameraUserComponent component, ComponentShutdown args)
    {
        _actionBlocker.UpdateCanMove(uid);
        _alerts.ClearAlert(uid, component.AlertPrototype);
    }

    private void OnUpdateCanMove(EntityUid uid, PhotoCameraUserComponent component, UpdateCanMoveEvent args)
    {
        if (component.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnCameraExamined(EntityUid uid, PhotoCameraComponent component, ExaminedEvent args)
    {
        var paperLeft = (int) MathF.Ceiling(_material.GetMaterialAmount(uid, component.CardMaterial) / component.CardCost);
        args.PushMarkup(Loc.GetString("photo-camera-examined-paper-left", ("count", paperLeft)));
    }
}
