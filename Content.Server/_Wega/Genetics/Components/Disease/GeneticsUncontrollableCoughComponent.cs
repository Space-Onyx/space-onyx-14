using System.Numerics;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Wega.Genetics.Components;

[RegisterComponent]
public sealed partial class GeneticsUncontrollableCoughComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EmotePrototype> Emote = "Cough";

    [DataField(required: true)]
    public Vector2 TimeBetweenIncidents;

    public float NextIncidentTime;
}
