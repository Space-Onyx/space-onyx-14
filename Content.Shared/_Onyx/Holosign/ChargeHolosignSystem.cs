using System.Linq;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.SubFloor;
using Robust.Shared.Physics.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Holosign;

public sealed partial class ChargeHolosignSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly HashSet<Entity<IComponent>> _signs = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChargeHolosignProjectorComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ChargeHolosignProjectorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChargeHolosignProjectorComponent, BeforeRangedInteractEvent>(OnBeforeInteract);
        SubscribeLocalEvent<ChargeHolosignProjectorComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnInit(Entity<ChargeHolosignProjectorComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        ent.Comp.SignComponent = EntityManager.ComponentFactory.GetRegistration(ent.Comp.SignComponentName).Type;
    }

    private void OnMapInit(Entity<ChargeHolosignProjectorComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<LimitedChargesComponent>(ent, out var charges))
            return;

        for (var i = 0; i < charges.MaxCharges; i++)
        {
            if (!TrySpawnInContainer(ent.Comp.SignProto, ent, ent.Comp.ContainerId, out var sign))
                return;

            ent.Comp.Signs.Add(sign.Value);
        }

        Dirty(ent);
    }

    private void OnBeforeInteract(Entity<ChargeHolosignProjectorComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (!_timing.IsFirstTimePredicted || args.Handled || !args.CanReach ||
            HasComp<StorageComponent>(args.Target) ||
            !TryComp<LimitedChargesComponent>(ent, out var charges))
            return;

        var coordinates = args.ClickLocation.SnapToGrid(EntityManager);
        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        _signs.Clear();
        _lookup.GetEntitiesInRange(ent.Comp.SignComponent, mapCoordinates, 0.25f, _signs);

        if (_signs.Count > 0)
            TryRemoveSign((ent, ent, charges), _signs.First(), args.User);
        else if (!HasBuildingOnTile(coordinates))
            TryPlaceSign((ent, ent, charges), coordinates, args.User);
        else
            _popup.PopupClient(Loc.GetString("charge-holoprojector-tile-occupied"), ent, args.User);

        args.Handled = true;
    }

    private bool HasBuildingOnTile(EntityCoordinates coordinates)
    {
        if (!_turf.TryGetTileRef(coordinates, out var targetTile))
            return true;

        foreach (var entity in _turf.GetEntitiesInTile(coordinates, LookupFlags.Uncontained))
        {
            if (HasComp<SubFloorHideComponent>(entity) || !HasComp<PhysicsComponent>(entity))
                continue;

            if (!_turf.TryGetTileRef(Transform(entity).Coordinates, out var entityTile) ||
                entityTile.Value.GridUid != targetTile.Value.GridUid ||
                entityTile.Value.GridIndices != targetTile.Value.GridIndices)
                continue;

            return true;
        }

        return false;
    }

    private void OnUseInHand(Entity<ChargeHolosignProjectorComponent> ent, ref UseInHandEvent args)
    {
        if (!_timing.IsFirstTimePredicted || !TryComp<LimitedChargesComponent>(ent, out var charges))
            return;

        var recalled = 0;
        var removed = new List<EntityUid>();
        foreach (var sign in ent.Comp.Signs)
        {
            if (TerminatingOrDeleted(sign))
            {
                removed.Add(sign);
                continue;
            }

            if (ent.Comp.Container.Contains(sign) || TryRemoveSign((ent, ent.Comp, charges), sign, args.User, false))
                recalled++;
            else
            {
                if (_net.IsServer)
                    QueueDel(sign);
                removed.Add(sign);
            }
        }

        foreach (var sign in removed)
            ent.Comp.Signs.Remove(sign);

        for (var i = recalled; i < charges.MaxCharges; i++)
        {
            if (!TrySpawnInContainer(ent.Comp.SignProto, ent, ent.Comp.ContainerId, out var sign))
                break;
            _charges.AddCharges((ent, charges), 1);
            ent.Comp.Signs.Add(sign.Value);
        }

        Dirty(ent);
    }

    private bool TryPlaceSign(Entity<ChargeHolosignProjectorComponent?, LimitedChargesComponent?> ent,
        EntityCoordinates coordinates, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2))
            return false;

        if (ent.Comp1.Container.Count == 0 || !_charges.TryUseCharge((ent, ent.Comp2)))
        {
            _popup.PopupClient(Loc.GetString("charge-holoprojector-no-charges", ("item", ent)), ent, user);
            return false;
        }

        var sign = ent.Comp1.Container.ContainedEntities.First();
        _transform.SetCoordinates(sign, coordinates);
        _transform.AnchorEntity(sign);
        return true;
    }

    private bool TryRemoveSign(Entity<ChargeHolosignProjectorComponent?, LimitedChargesComponent?> ent,
        EntityUid sign, EntityUid user, bool showIdentity = true)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2))
            return false;

        if (_charges.GetCurrentCharges((ent, ent.Comp2, null)) >= ent.Comp2.MaxCharges)
        {
            _popup.PopupClient(Loc.GetString("charge-holoprojector-charges-full", ("item", ent)), sign, user);
            return false;
        }

        if (!_container.Insert(sign, ent.Comp1.Container, force: true))
            return false;

        _charges.AddCharges((ent, ent.Comp2), 1);
        var others = showIdentity
            ? Loc.GetString("charge-holoprojector-reclaim-others", ("sign", sign), ("user", Identity.Name(user, EntityManager)))
            : Loc.GetString("charge-holoprojector-recall-others", ("sign", sign));
        _popup.PopupPredicted(Loc.GetString("charge-holoprojector-reclaim", ("sign", sign)), others, ent, user);
        return true;
    }
}
