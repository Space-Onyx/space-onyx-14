/*
 * License: MIT
 * Copyright: (c) 2025 TornadoTechnology
 */

using Robust.Shared.Prototypes;

namespace Content.Client._TT.AdditionalMap;

[Prototype("additionalMap")]
public sealed partial class TTAdditionalMapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;
}
