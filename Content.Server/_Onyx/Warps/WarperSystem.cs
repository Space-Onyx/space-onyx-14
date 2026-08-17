using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Ghost.Components;
using Content.Shared.Interaction;
using Content.Shared.Warps;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Onyx.Warps;

public sealed partial class WarperSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarperComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(Entity<WarperComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || string.IsNullOrWhiteSpace(ent.Comp.Id))
        {
            if (!args.Handled)
                ShowFailure(ent, args.User);

            return;
        }

        TransformComponent? destination = null;
        foreach (var (point, transform) in EntityQuery<WarpPointComponent, TransformComponent>(true))
        {
            if (point.Id != ent.Comp.Id)
                continue;

            destination = transform;
            break;
        }

        if (destination is null || !Exists(destination.Owner))
        {
            ShowFailure(ent, args.User);
            return;
        }

        var destinationMap = destination.MapID;
        if ((!_mapSystem.IsInitialized(destinationMap) || _mapSystem.IsPaused(destinationMap))
            && !HasComp<GhostComponent>(args.User))
        {
            ShowFailure(ent, args.User);
            return;
        }

        _transform.SetCoordinates(args.User, destination.Coordinates);
        _transform.AttachToGridOrMap(args.User);

        if (TryComp<PhysicsComponent>(args.User, out var physics))
            _physics.SetLinearVelocity(args.User, Vector2.Zero, body: physics);

        args.Handled = true;
    }

    private void ShowFailure(Entity<WarperComponent> ent, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", ent.Owner)), user, user);
    }
}
