using Robust.Shared.Prototypes;
using Content.Shared.Tag;

namespace Content.Shared.Movement.Components;

public sealed partial class SpeedModifierContactsComponent
{
    [DataField, AutoNetworkedField]
    public ProtoId<TagPrototype>? IgnoreWhenContactingTag;
}
