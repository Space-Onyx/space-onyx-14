using Content.Server.Power.Components;
using Content.Shared._Onyx.Shuttles.Components;
using Content.Shared.Power;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.Shuttles.Systems;

public sealed partial class FTLDriveSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;

    private readonly HashSet<Entity<FTLDriveGeneratorComponent>> _drives = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FTLDriveGeneratorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FTLDriveGeneratorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FTLDriveGeneratorComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<FTLDriveGeneratorComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<FTLDriveGeneratorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
    }

    private void OnGridInit(GridInitializeEvent args)
    {
        if (!HasComp<MapComponent>(args.EntityUid))
        {
            EnsureComp<FTLDriveComponent>(args.EntityUid);
            RefreshGrid(args.EntityUid);
        }
    }

    private void OnStartup(Entity<FTLDriveGeneratorComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Ready = TryComp<ApcPowerReceiverComponent>(ent, out var power) &&
                         power.Powered &&
                         Transform(ent).Anchored;
        RefreshGrid(Transform(ent).GridUid);
    }

    private void OnShutdown(Entity<FTLDriveGeneratorComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Ready = false;
        RefreshGrid(Transform(ent).GridUid);
    }

    private void OnParentChanged(Entity<FTLDriveGeneratorComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshGrid(args.OldParent);
        RefreshGrid(Transform(ent).GridUid);
    }

    private void OnAnchorChanged(Entity<FTLDriveGeneratorComponent> ent, ref AnchorStateChangedEvent args)
    {
        ent.Comp.Ready = args.Anchored &&
                         TryComp<ApcPowerReceiverComponent>(ent, out var power) &&
                         power.Powered;
        RefreshGrid(Transform(ent).GridUid);
    }

    private void OnPowerChanged(Entity<FTLDriveGeneratorComponent> ent, ref PowerChangedEvent args)
    {
        ent.Comp.Ready = args.Powered && Transform(ent).Anchored;
        RefreshGrid(Transform(ent).GridUid);
    }

    private void RefreshGrid(EntityUid? gridUid)
    {
        if (gridUid is not { } grid || !TryComp<FTLDriveComponent>(grid, out var drive))
            return;

        _drives.Clear();
        _lookup.GetGridEntities(grid, _drives);

        Entity<FTLDriveGeneratorComponent>? selected = null;
        foreach (var candidate in _drives)
        {
            if (!candidate.Comp.Ready)
                continue;

            if (selected == null ||
                candidate.Comp.Priority > selected.Value.Comp.Priority ||
                candidate.Comp.Priority == selected.Value.Comp.Priority && candidate.Owner.Id < selected.Value.Owner.Id)
            {
                selected = candidate;
            }
        }

        var data = selected?.Comp.Data ?? FTLDriveComponent.DefaultData;
        if (drive.Data == data)
            return;

        drive.Data = data;
        Dirty(grid, drive);
    }
}
