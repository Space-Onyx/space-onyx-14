using Content.Shared._Onyx.Language;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Traits;

[RegisterComponent]
public sealed partial class LanguageTraitComponent : Component
{
    [DataField(required: true)]
    public ProtoId<LanguagePrototype> Language;
}
