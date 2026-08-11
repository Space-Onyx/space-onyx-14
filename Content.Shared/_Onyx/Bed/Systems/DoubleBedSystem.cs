using System.Numerics;
using Content.Shared._Onyx.Bed.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction;
using Content.Shared.Placeable;
using Content.Shared.Tag;

namespace Content.Shared._Onyx.Bed.Systems;

public sealed partial class DoubleBedSystem : EntitySystem
{
    private const string BedsheetTag = "Bedsheet";

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private PlaceableSurfaceSystem _placeableSurface = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<TagComponent>> _nearbyBedsheets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DoubleBedComponent, ComponentStartup>(OnDoubleBedStartup);
        SubscribeLocalEvent<DoubleBedComponent, StrapAttemptEvent>(OnStrapAttempt, before: [typeof(SharedBuckleSystem)]);
        SubscribeLocalEvent<DoubleBedComponent, UnstrappedEvent>(OnUnstrapped, after: [typeof(SharedBuckleSystem)]);
        SubscribeLocalEvent<DoubleBedComponent, AfterInteractUsingEvent>(OnAfterInteractUsing, before: [typeof(PlaceableSurfaceSystem)]);
        SubscribeLocalEvent<DoubleBedComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(Entity<DoubleBedComponent> ent, ref InteractHandEvent args)
    {
        var childEnumerator = Transform(ent).ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (!HasComp<DoubleBedSheetComponent>(child))
                continue;

            args.Handled = true;
            _transform.SetCoordinates(child, Transform(args.User).Coordinates);
            return;
        }
    }

    private void OnDoubleBedStartup(Entity<DoubleBedComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<StrapComponent>(ent, out var strap) || strap.BuckleOffset != Vector2.Zero)
            return;

        strap.BuckleOffset = ent.Comp.LeftOffset;
        Dirty(ent, strap);
    }

    private void OnStrapAttempt(Entity<DoubleBedComponent> ent, ref StrapAttemptEvent args)
    {
        if (!TryComp<StrapComponent>(ent, out var strap))
            return;

        var leftOccupied = false;
        foreach (var buckled in strap.BuckledEntities)
        {
            if (strap.BuckleOffsets.GetValueOrDefault(buckled, ent.Comp.LeftOffset) != ent.Comp.LeftOffset)
                continue;

            leftOccupied = true;
            break;
        }

        var offset = leftOccupied ? ent.Comp.RightOffset : ent.Comp.LeftOffset;
        strap.BuckleOffsets[args.Buckle.Owner] = offset;
        strap.BuckleOffset = offset;
        Dirty(ent, strap);
    }

    private void OnUnstrapped(Entity<DoubleBedComponent> ent, ref UnstrappedEvent args)
    {
        if (!TryComp<StrapComponent>(ent, out var strap))
            return;

        strap.BuckleOffset = ent.Comp.LeftOffset;
        foreach (var buckled in strap.BuckledEntities)
        {
            strap.BuckleOffset = strap.BuckleOffsets.GetValueOrDefault(buckled, ent.Comp.LeftOffset);
            break;
        }

        Dirty(ent, strap);
    }

    private void OnAfterInteractUsing(Entity<DoubleBedComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (!TryComp<PlaceableSurfaceComponent>(ent, out var surface) ||
            !_tagSystem.HasTag(args.Used, BedsheetTag))
        {
            return;
        }

        var isDoubleBedsheet = HasComp<DoubleBedSheetComponent>(args.Used);
        var bedCoords = Transform(ent).Coordinates;
        var bedsheetCount = 0;

        _nearbyBedsheets.Clear();
        _lookup.GetEntitiesInRange(bedCoords, 0.5f, _nearbyBedsheets);
        foreach (var (uid, _) in _nearbyBedsheets)
        {
            if (uid == args.Used || !_tagSystem.HasTag(uid, BedsheetTag))
                continue;

            if (Transform(uid).Coordinates.TryDistance(EntityManager, bedCoords, out var distance) && distance < 0.5f)
                bedsheetCount++;
        }

        if (isDoubleBedsheet || HasChildDoubleBedsheet(ent))
            return;

        var offset = bedsheetCount == 0
            ? ent.Comp.RightBedsheetOffset
            : ent.Comp.LeftBedsheetOffset;
        _placeableSurface.SetPositionOffset(ent, offset, surface);
    }

    private bool HasChildDoubleBedsheet(EntityUid uid)
    {
        var childEnumerator = Transform(uid).ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (HasComp<DoubleBedSheetComponent>(child))
                return true;
        }

        return false;
    }
}
