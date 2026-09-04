// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
namespace Content.Shared.Vampire.Components;

/// <summary>
/// A component for testing vampire arson near holy sites.
/// </summary>
[RegisterComponent]
public sealed partial class HolyPointComponent : Component
{
    [DataField]
    public float Range = 6f;

    public float NextTimeTick { get; set; }
}
