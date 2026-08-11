using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.MobCall;

[RegisterComponent]
public sealed partial class MobCallSourceComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public float Range = 20f;

    [DataField]
    public ProtoId<EmotePrototype> Emote = "Scream";
}

[RegisterComponent]
public sealed partial class MobCallableComponent : Component;

public sealed partial class MobCallActionEvent : InstantActionEvent;
