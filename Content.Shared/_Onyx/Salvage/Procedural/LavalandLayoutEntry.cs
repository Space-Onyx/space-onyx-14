using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Salvage.Procedural;

[DataRecord]
public partial record struct LavalandLayoutEntry(ResPath GridPath, Vector2 Position, LocId Name);
