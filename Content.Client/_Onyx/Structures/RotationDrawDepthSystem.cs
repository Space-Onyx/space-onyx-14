using Robust.Client.GameObjects;
using Content.Shared._Onyx.Structures;

namespace Content.Client._Onyx.Structures;

public sealed partial class RotationDrawDepthSystem : EntitySystem
{
    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<RotationDrawDepthComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out _, out var rotation, out var sprite, out var transform))
        {
            sprite.DrawDepth = transform.LocalRotation.GetCardinalDir() == Direction.South
                ? rotation.SouthDrawDepth
                : rotation.DefaultDrawDepth;
        }
    }
}
