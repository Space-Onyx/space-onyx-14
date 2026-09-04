// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
namespace Content.Shared.Vampire.Components;

[RegisterComponent, Access(typeof(SharedVampireSystem))]
public sealed partial class VampirePolymorphComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Body;
}
