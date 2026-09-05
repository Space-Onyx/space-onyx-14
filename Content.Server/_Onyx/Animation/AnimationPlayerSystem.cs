using Content.Shared._Onyx.AnimationData;
using Robust.Shared.Player;

namespace Content.Server._Onyx.AnimationData;

public sealed partial class AnimationPlayerSystem : EntitySystem
{
    public void PlayAnimation(EntityUid entity, string animation) =>
        RaiseNetworkEvent(new PlayAnimationMessage(GetNetEntity(entity), animation));

    public void PlayAnimation(EntityUid entity, string animation, Filter filter) =>
        RaiseNetworkEvent(new PlayAnimationMessage(GetNetEntity(entity), animation), filter);
}
