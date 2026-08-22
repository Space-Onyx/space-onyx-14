// SPDX-FileCopyrightText: 2024 Milon <milonpl.git@proton.me>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Skubman <ba.fallaria@gmail.com>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.NameIdentifier;
using Content.Shared._DV.NanoChat;
using Content.Shared.NameIdentifier;
using Content.Shared.PDA;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._DV.NanoChat;

/// <summary>
///     Handles NanoChat features that are specific to the server but not related to the cartridge itself.
/// </summary>
public sealed partial class NanoChatSystem : SharedNanoChatSystem
{
    [Dependency] private NameIdentifierSystem _name = default!;

    private readonly ProtoId<NameIdentifierGroupPrototype> _nameIdentifierGroup = "NanoChat";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NanoChatCardComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<NanoChatCardComponent, EntGotRemovedFromContainerMessage>(OnRemoved);

        SubscribeLocalEvent<NanoChatCardComponent, MapInitEvent>(OnCardInit);
    }

    private void OnInserted(Entity<NanoChatCardComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != PdaComponent.PdaIdSlotId)
            return;

        ent.Comp.PdaUid = args.Container.Owner;
        Dirty(ent);
    }

    private void OnRemoved(Entity<NanoChatCardComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != PdaComponent.PdaIdSlotId)
            return;

        ent.Comp.PdaUid = null;
        Dirty(ent);
    }

    private void OnCardInit(Entity<NanoChatCardComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Number != null)
            return;

        // Assign a random number
        _name.GenerateUniqueNameModifier(_nameIdentifierGroup, out var number);
        ent.Comp.Number = (uint)number;
        Dirty(ent);
    }
}