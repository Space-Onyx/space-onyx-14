// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class MindCommunicationGenComponent : Component
{
    public readonly EntProtoId Action = "ActionMindCommunicationGen";

    public EntityUid? ActionEntity { get; set; }
}
