using Content.Shared._Onyx.Bloodtrak;
using Content.Shared.Pinpointer;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Onyx.Bloodtrak;

public sealed partial class BloodtrakSystem : SharedBloodtrakSystem
{
    [Dependency] private IEyeManager _eye = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BloodtrakComponent, SpriteComponent>();
        while (query.MoveNext(out var tracker, out var sprite))
        {
            if (tracker.DistanceToTarget is BloodtrakDistance.Close or BloodtrakDistance.Medium or BloodtrakDistance.Far)
                sprite.LayerSetRotation(PinpointerLayers.Screen, tracker.ArrowAngle + _eye.CurrentEye.Rotation);
            else
                sprite.LayerSetRotation(PinpointerLayers.Screen, Angle.Zero);
        }
    }
}
