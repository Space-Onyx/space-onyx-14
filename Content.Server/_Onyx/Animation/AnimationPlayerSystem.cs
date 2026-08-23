using Content.Shared._Onyx.AnimationData;

namespace Content.Server._Onyx.AnimationData;

public sealed partial class AnimationPlayerSystem : EntitySystem
{
    public void PlayAnimation(EntityUid entity, string animation) =>
        RaiseNetworkEvent(new PlayAnimationMessage(GetNetEntity(entity), animation));
}
