// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using System.Numerics;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Genetics.Components;

[RegisterComponent]
public sealed partial class GeneticsUncontrollableCoughComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EmotePrototype> Emote = "Cough";

    [DataField(required: true)]
    public Vector2 TimeBetweenIncidents;

    public float NextIncidentTime;
}
