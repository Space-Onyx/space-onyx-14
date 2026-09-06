// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;
using Content.Shared.Salvage;
using Robust.Shared.Map;

#pragma warning disable IDE0130
namespace Content.Server.Salvage;

public sealed partial class RestrictedRangeSystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float BoundaryInset = 0.5f;
    private readonly HashSet<EntityUid> _boundaryRollbacks = new();

    private void InitializeAbsoluteBoundary()
    {
        SubscribeLocalEvent<TransformComponent, MoveEvent>(OnEntityMoved);
    }

    private void OnEntityMoved(Entity<TransformComponent> ent, ref MoveEvent args)
    {
        if (args.OnlyRotation || !_boundaryRollbacks.Add(ent.Owner))
            return;

        try
        {
            if (ent.Comp.MapUid is not { } mapUid ||
                !TryComp<RestrictedRangeComponent>(mapUid, out var restricted))
                return;

            var newPosition = _transform.ToMapCoordinates(args.NewPosition).Position;
            var offset = newPosition - restricted.Origin;
            if (offset.LengthSquared() <= restricted.Range * restricted.Range)
                return;

            if (args.OldPosition.IsValid(EntityManager))
            {
                var oldCoordinates = _transform.ToMapCoordinates(args.OldPosition);
                var oldOffset = oldCoordinates.Position - restricted.Origin;
                if (oldCoordinates.MapId == ent.Comp.MapID &&
                    oldOffset.LengthSquared() <= restricted.Range * restricted.Range)
                {
                    _transform.SetCoordinates(args.Entity, args.OldPosition);
                    return;
                }
            }

            var maxRange = MathF.Max(0f, restricted.Range - BoundaryInset);
            var position = offset == Vector2.Zero
                ? restricted.Origin
                : restricted.Origin + Vector2.Normalize(offset) * maxRange;
            _transform.SetCoordinates(args.Entity, new EntityCoordinates(mapUid, position));
        }
        finally
        {
            _boundaryRollbacks.Remove(ent.Owner);
        }
    }
}
