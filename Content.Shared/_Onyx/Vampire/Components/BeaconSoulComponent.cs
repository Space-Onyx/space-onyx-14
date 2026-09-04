// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
namespace Content.Shared.Vampire.Components;

/// <summary>
/// Just a chill guy.
/// Defines an entity as a beacon of the soul.
/// </summary>
[RegisterComponent]
public sealed partial class BeaconSoulComponent : Component
{
    [DataField]
    public EntityUid VampireOwner = default!;
}
