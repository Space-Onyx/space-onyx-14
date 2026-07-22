// SPDX-FileCopyrightText: 2025 Conchelle <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ted Lukin <66275205+pheenty@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.SetSelector;

[Prototype]
public sealed partial class SelectableSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; private set; } = string.Empty;

    [DataField]
    public string Description { get; private set; } = string.Empty;

    [DataField]
    public SpriteSpecifier Sprite { get; private set; } = SpriteSpecifier.Invalid;

    [DataField]
    public List<EntProtoId> Content = new();

    [DataField]
    public List<ProtoId<EntityTablePrototype>> Tables = new();
}
