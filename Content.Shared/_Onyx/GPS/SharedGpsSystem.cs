using Content.Shared.UserInterface;

namespace Content.Shared._Onyx.GPS;

public abstract partial class SharedGpsSystem : EntitySystem
{
    public const int MaxNameLength = 32;

    [Dependency] protected SharedUserInterfaceSystem UiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GPSComponent, BeforeActivatableUIOpenEvent>(OnOpen);
        SubscribeLocalEvent<GPSComponent, GpsSetTrackedEntityMessage>(OnSetTrackedEntity);
        SubscribeLocalEvent<GPSComponent, GpsSetGpsNameMessage>(OnSetGpsName);
        SubscribeLocalEvent<GPSComponent, GpsSetInDistressMessage>(OnSetInDistress);
        SubscribeLocalEvent<GPSComponent, GpsSetEnabledMessage>(OnSetEnabled);
    }

    private void OnOpen(Entity<GPSComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        Dirty(ent);
    }

    private void OnSetTrackedEntity(Entity<GPSComponent> ent, ref GpsSetTrackedEntityMessage args)
    {
        if (!CanTrack(ent, args.NetEntity))
            return;

        ent.Comp.TrackedEntity = args.NetEntity;
        DirtyField(ent, ent.Comp, nameof(GPSComponent.TrackedEntity));
    }

    private void OnSetGpsName(Entity<GPSComponent> ent, ref GpsSetGpsNameMessage args)
    {
        ent.Comp.GpsName = args.GpsName.Trim()[..Math.Min(args.GpsName.Trim().Length, MaxNameLength)];
        DirtyField(ent, ent.Comp, nameof(GPSComponent.GpsName));
    }

    private void OnSetInDistress(Entity<GPSComponent> ent, ref GpsSetInDistressMessage args)
    {
        ent.Comp.InDistress = args.InDistress;
        DirtyField(ent, ent.Comp, nameof(GPSComponent.InDistress));
    }

    private void OnSetEnabled(Entity<GPSComponent> ent, ref GpsSetEnabledMessage args)
    {
        ent.Comp.Enabled = args.Enabled;
        DirtyField(ent, ent.Comp, nameof(GPSComponent.Enabled));
    }

    protected virtual bool CanTrack(Entity<GPSComponent> ent, NetEntity? trackedEntity)
    {
        return trackedEntity == null;
    }
}
