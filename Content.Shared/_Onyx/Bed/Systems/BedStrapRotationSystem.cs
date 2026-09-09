using Content.Shared.Bed.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Rotation;
using Robust.Shared.GameObjects;

namespace Content.Shared._Onyx.Bed.Systems;

public sealed partial class BedStrapRotationSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedRotationVisualsSystem _rotationVisuals = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrapComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<StrapComponent, UnstrappedEvent>(OnUnstrapped);
        _transform.OnGlobalMoveEvent += OnGlobalMove;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _transform.OnGlobalMoveEvent -= OnGlobalMove;
    }

    private void OnStrapped(Entity<StrapComponent> ent, ref StrappedEvent args)
    {
        if (!HasComp<HealOnBuckleComponent>(ent.Owner))
            return;

        UpdateBuckled(ent, args.Buckle.Owner, Transform(ent.Owner).LocalRotation);
    }

    private void OnUnstrapped(Entity<StrapComponent> ent, ref UnstrappedEvent args)
    {
        if (!HasComp<HealOnBuckleComponent>(ent.Owner))
            return;

        if (Transform(args.Buckle.Owner).ParentUid == ent.Owner)
            return;

        _transform.SetLocalRotation(args.Buckle.Owner, Angle.Zero);
    }

    private void OnGlobalMove(ref MoveEvent args)
    {
        if (args.OldRotation.Equals(args.NewRotation))
            return;

        if (!TryComp<StrapComponent>(args.Sender, out var strap))
            return;

        if (!HasComp<HealOnBuckleComponent>(args.Sender))
            return;

        if (strap.BuckledEntities.Count == 0)
            return;

        var ent = new Entity<StrapComponent>(args.Sender, strap);
        foreach (var buckled in strap.BuckledEntities)
        {
            UpdateBuckled(ent, buckled, args.NewRotation);
        }
    }

    private void UpdateBuckled(Entity<StrapComponent> strap, EntityUid buckled, Angle relative)
    {
        _rotationVisuals.SetHorizontalAngle(buckled, strap.Comp.Rotation + relative);
        _transform.SetLocalRotation(buckled, -relative);
    }
}
