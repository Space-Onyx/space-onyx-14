using Content.Shared._Onyx.Research;
using Content.Shared.Research.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Research.Components;

public sealed partial class ResearchServerComponent
{
    /// <summary>
    /// Typed point balances of the network authority. <see cref="Points"/> mirrors the General balance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ResearchPointAmount> PointBalances = new() { new(ResearchPointAmount.General, 0) };
}

/// <summary>
/// Event raised on a server's clients when the balance of a specific point type changes.
/// </summary>
[ByRefEvent]
public readonly record struct ResearchServerPointTypeChangedEvent(EntityUid Server, string Type, int Total, int Delta);
