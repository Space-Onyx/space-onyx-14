using Robust.Shared.Map;

namespace Content.Server._Onyx.SpaceWhale;

public sealed partial class TailedEntitySystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TailedEntityComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<TailedEntityComponent> ent, ref ComponentShutdown args)
    {
        DeleteSegments(ent.Comp);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TailedEntityComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (component.TailSegments.Count != component.Amount || component.TailSegments.Exists(segment => !Exists(segment)))
            {
                DeleteSegments(component);
                InitializeSegments(component, xform);
                continue;
            }

            UpdateSegments(component, xform, frameTime);
        }
    }

    private void DeleteSegments(TailedEntityComponent component)
    {
        foreach (var segment in component.TailSegments)
            QueueDel(segment);

        component.TailSegments.Clear();
    }

    private void InitializeSegments(TailedEntityComponent component, TransformComponent xform)
    {
        if (xform.MapUid is not { } mapUid)
            return;

        var headPosition = _transform.GetWorldPosition(xform);
        var headRotation = _transform.GetWorldRotation(xform);
        for (var i = 0; i < component.Amount; i++)
        {
            var offset = headRotation.ToWorldVec() * component.Spacing * (i + 1);
            component.TailSegments.Add(Spawn(component.Prototype, new EntityCoordinates(mapUid, headPosition - offset)));
        }
    }

    private void UpdateSegments(TailedEntityComponent component, TransformComponent xform, float frameTime)
    {
        var headPosition = _transform.GetWorldPosition(xform);
        var headRotation = _transform.GetWorldRotation(xform);
        for (var i = 0; i < component.TailSegments.Count; i++)
        {
            var segment = component.TailSegments[i];
            if (!TryComp(segment, out TransformComponent? segmentXform))
                continue;

            var target = headPosition - headRotation.ToWorldVec() * component.Spacing * (i + 1);
            var current = _transform.GetWorldPosition(segmentXform);
            var difference = target - current;
            var distance = difference.Length();
            var newPosition = distance < component.Spacing * 0.1f
                ? target
                : current + difference.Normalized() * MathF.Min(component.Speed * frameTime, distance);
            _transform.SetWorldPosition(segment, newPosition);
        }

        for (var i = 0; i < component.TailSegments.Count; i++)
        {
            var segment = component.TailSegments[i];
            if (!TryComp(segment, out TransformComponent? segmentXform))
                continue;

            var targetPosition = i == 0
                ? headPosition
                : _transform.GetWorldPosition(Transform(component.TailSegments[i - 1]));
            var direction = targetPosition - _transform.GetWorldPosition(segmentXform);
            var targetRotation = direction.ToWorldAngle();
            var rotation = Angle.Lerp(_transform.GetWorldRotation(segmentXform), targetRotation, component.Speed * frameTime * 2f);
            _transform.SetWorldRotation(segment, rotation);
        }
    }
}
