// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Vampire;

[Serializable, NetSerializable]
public sealed partial class VampireClassSelectionState : EuiStateBase
{
}

[Serializable, NetSerializable]
public sealed partial class VampireClassSelectedMessage : EuiMessageBase
{
    public VampireClassEnum SelectedClass { get; }
    public VampireClassSelectedMessage(VampireClassEnum selectedClass) => SelectedClass = selectedClass;
}
