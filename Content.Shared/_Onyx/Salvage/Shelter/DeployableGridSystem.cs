using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Map.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Salvage.Shelter;

public abstract partial class SharedDeployableGridSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeployableGridComponent, UseInHandEvent>(OnUse);
    }

    private void OnUse(Entity<DeployableGridComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !CheckCanDeploy(ent))
        {
            args.Handled = true;
            return;
        }
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime,
            new DeployableGridDoAfterEvent(), ent, used: ent) { BreakOnMove = true, NeedHand = true });
        args.Handled = true;
    }

    protected bool CheckCanDeploy(Entity<DeployableGridComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.GridUid == null || xform.MapUid == null || xform.GridUid != xform.MapUid ||
            !TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
        {
            _popup.PopupCoordinates(Loc.GetString("shelter-capsule-fail-no-planet"), xform.Coordinates);
            return false;
        }
        if (_lookup.GetEntitiesInRange<MapGridComponent>(xform.Coordinates, ent.Comp.BoxSize.Length())
            .Any(uid => uid.Owner != xform.GridUid.Value))
        {
            _popup.PopupCoordinates(Loc.GetString("shelter-capsule-fail-near-grid"), xform.Coordinates);
            return false;
        }
        var pos = _transform.GetMapCoordinates(ent).Position.Rounded();
        if (_map.GetAnchoredEntities(xform.GridUid.Value, grid, Box2.CenteredAround(pos, ent.Comp.BoxSize)).Any())
        {
            _popup.PopupCoordinates(Loc.GetString("shelter-capsule-fail-no-space"), xform.Coordinates);
            return false;
        }
        return true;
    }
}

[Serializable, NetSerializable]
public sealed partial class DeployableGridDoAfterEvent : SimpleDoAfterEvent;
