// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Structures.Sauna;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SaunaOvenComponent : Component
{
    /// <summary>Maximum fuel accepted from wood.</summary>
    [DataField]
    public float MaximumFuel = 3000f;

    /// <summary>Fuel supplied by one wood plank.</summary>
    [DataField]
    public float WoodFuel = 150f;

    /// <summary>Fuel supplied by one sheet of paper.</summary>
    [DataField]
    public float PaperFuel = 5f;

    /// <summary>Stored steam potential supplied by each water unit.</summary>
    [DataField]
    public float WaterSteamMultiplier = 5f;

    /// <summary>Fuel consumed per second while lit.</summary>
    [DataField]
    public float FuelConsumptionPerSecond = 0.5f;

    /// <summary>Stored steam potential consumed per second while lit.</summary>
    [DataField]
    public float WaterConsumptionPerSecond = 0.5f;

    /// <summary>Water vapor added to atmosphere per second while water remains.</summary>
    [DataField]
    public float SteamMolesPerSecond = 12.5f;

    /// <summary>Interval between fuel and steam processing cycles.</summary>
    [DataField]
    public float ProcessInterval = 2f;

    /// <summary>Temperature of emitted water vapor in kelvin.</summary>
    [DataField]
    public float SteamTemperature = 343.15f;

    /// <summary>Pressure above which no more vapor enters atmosphere.</summary>
    [DataField]
    public float MaximumSteamPressure = 202.65f;

    /// <summary>Visual effect spawned while steam is emitted.</summary>
    [DataField]
    public EntProtoId SteamEffect = "EffectSaunaSteam";

    /// <summary>Remaining fuel.</summary>
    [AutoNetworkedField]
    public float Fuel;

    /// <summary>Remaining steam potential.</summary>
    [AutoNetworkedField]
    public float Water;

    /// <summary>Whether oven is burning.</summary>
    [AutoNetworkedField]
    public bool Lit;

    /// <summary>Server processing accumulator.</summary>
    public float ProcessAccumulator;
}

[RegisterComponent]
public sealed partial class ActiveSaunaOvenComponent : Component;
