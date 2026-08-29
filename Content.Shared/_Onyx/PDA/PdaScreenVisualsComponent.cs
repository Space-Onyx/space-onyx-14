using Robust.Shared.Utility;

namespace Content.Shared._Onyx.PDA;

[RegisterComponent]
public sealed partial class PdaScreenVisualsComponent : Component
{
    [DataField(required: true)]
    public SpriteSpecifier IdleScreen = default!;

    [DataField(required: true)]
    public SpriteSpecifier MenuScreen = default!;
}
