using Content.Shared.Chat.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Emoting;

[Serializable, NetSerializable, DataDefinition] public sealed partial class AnimationFlipEmoteEvent : EntityEventArgs { }
[Serializable, NetSerializable, DataDefinition] public sealed partial class AnimationSpinEmoteEvent : EntityEventArgs { }
[Serializable, NetSerializable, DataDefinition] public sealed partial class AnimationJumpEmoteEvent : EntityEventArgs { }
[Serializable, NetSerializable, DataDefinition] public sealed partial class AnimationTweakEmoteEvent : EntityEventArgs { }
[Serializable, NetSerializable, DataDefinition] public sealed partial class AnimationFlexEmoteEvent : EntityEventArgs { }

[RegisterComponent, NetworkedComponent]
public sealed partial class AnimatedEmotesComponent : Component
{
    [DataField] public ProtoId<EmotePrototype>? Emote;
}

[Serializable, NetSerializable]
public sealed partial class AnimatedEmotesComponentState(ProtoId<EmotePrototype>? emote) : ComponentState
{
    public ProtoId<EmotePrototype>? Emote = emote;
}
