using Content.Shared.Metabolism;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Feroxi;

[RegisterComponent]
public sealed partial class FeroxiDehydrateComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MetabolizerTypePrototype> HydratedMetabolizer;

    [DataField(required: true)]
    public ProtoId<MetabolizerTypePrototype> DehydratedMetabolizer;

    [DataField]
    public float DehydrationThreshold = 150f;

    [DataField]
    public bool Dehydrated;
}
