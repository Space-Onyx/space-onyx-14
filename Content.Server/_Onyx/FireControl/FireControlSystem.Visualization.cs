using System.Linq;
using System.Numerics;
using Content.Shared._Onyx.FireControl;
using Content.Shared.Physics;
using Robust.Shared.Physics;

namespace Content.Server._Onyx.FireControl;

public sealed partial class FireControlSystem
{
    private readonly HashSet<EntityUid> _visualizedEntities = new();

    public Dictionary<float, bool> CheckAllDirections(EntityUid weapon, float maxDistance = 500f, int rayCount = 256)
    {
        var directions = new Dictionary<float, bool>();
        var transform = Transform(weapon);
        var position = _transform.GetWorldPosition(transform);
        var mapId = transform.MapID;
        var weaponGrid = transform.GridUid;

        bool IgnoreEntityNotOnSameGrid(EntityUid entity, EntityUid source)
        {
            return entity == source || weaponGrid != null && Transform(entity).GridUid != weaponGrid;
        }

        for (var i = 0; i < rayCount; i++)
        {
            var angle = i / (float) rayCount * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var ray = new CollisionRay(position, direction, (int) (CollisionGroup.Opaque | CollisionGroup.Impassable));
            var blocked = _physics.IntersectRayWithPredicate(mapId, ray, weapon, IgnoreEntityNotOnSameGrid,
                maxDistance, false).Any();
            directions[angle * 180f / MathF.PI] = !blocked;
        }

        return directions;
    }

    public bool ToggleVisualization(EntityUid entityUid)
    {
        var netEntity = GetNetEntity(entityUid);
        if (_visualizedEntities.Remove(entityUid))
        {
            RaiseNetworkEvent(new FireControlVisualizationEvent(netEntity));
            return false;
        }

        _visualizedEntities.Add(entityUid);
        RaiseNetworkEvent(new FireControlVisualizationEvent(netEntity, CheckAllDirections(entityUid)));
        return true;
    }
}
