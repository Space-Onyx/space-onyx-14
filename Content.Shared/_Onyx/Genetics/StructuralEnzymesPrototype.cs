// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Genetics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

[Prototype, Access(typeof(SharedDnaModifierSystem))]
public sealed partial class StructuralEnzymesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = string.Empty;

    [DataField("message")]
    public string Message { get; set; } = default!;

    [DataField("addComponent")]
    public ComponentRegistry? AddComponent { get; private set; } = default!;

    [DataField("costInstability")]
    public int CostInstability { get; set; } = 0;

    [DataField("chanceAssimilation")]
    public float ChanceAssimilation { get; set; } = 1.0f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EnzymesType TypeDeviation = default!;
}

[Serializable, NetSerializable]
public enum EnzymesType : byte
{
    Disease,
    Minor,
    Intermediate,
    Base,
}
