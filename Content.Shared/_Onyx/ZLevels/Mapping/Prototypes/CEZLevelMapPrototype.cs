/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.ZLevels.Mapping.Prototypes;

[Prototype("zMap")]
public sealed partial class CEZLevelMapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Map resource paths ordered from bottom (depth 0) to top.
    /// </summary>
    [DataField]
    public List<ResPath> Maps { get; private set; } = new();

    /// <summary>
    /// Components applied to every map in the Z-network.
    /// </summary>
    [DataField]
    public ComponentRegistry Components { get; private set; } = new();
}
