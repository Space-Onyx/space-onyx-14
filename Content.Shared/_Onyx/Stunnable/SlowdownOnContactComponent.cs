// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Stunnable;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedStunSystem))]
public sealed partial class SlowdownOnContactComponent : Component
{
    [DataField]
    public string FixtureId = "fix";

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    [DataField]
    public float Multiplier = 1f;

    [DataField]
    public EntityWhitelist Blacklist = new();
}
