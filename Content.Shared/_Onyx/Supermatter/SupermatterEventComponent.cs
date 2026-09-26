// Content adapted from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/7076), licensed under AGPL-3.0-or-later.

using Content.Shared.Atmos;
using Content.Shared.Radio;
using Content.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared._GoobStation.Supermatter.Components;

public sealed partial class SupermatterComponent
{
    [DataField]
    public float HarshEventThreshold = 5f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float SMAngerValue;

    [ViewVariables(VVAccess.ReadWrite)]
    public float SMEventSetpoint = 2000f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float SMLastAnger;

    [DataField]
    public ProtoId<WeightedRandomPrototype> HarshEvents = "HarshEvents";

    [DataField]
    public ProtoId<WeightedRandomPrototype> NormalEvents = "NormalEvents";

    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Engineering";

    [DataField]
    public double TimeToUnlock = 5;

    public double TimeLocked;

    [DataField]
    public float RadiationOutputFactorSetpoint = 0.03f;

    [DataField]
    public bool RadiationOutputFactorChanged;

    [DataField]
    public float GasEfficiencySetpoint = 0.15f;

    [DataField]
    public bool GasEfficiencyFactorChanged;

    [DataField]
    public bool Surge;
}

[Prototype]
public sealed partial class SupermatterEventPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField]
    public Gas? GasToSpawn;

    [DataField]
    public EntProtoId? ProtoToSpawn;

    [DataField]
    public LocId? Announcement;
}
