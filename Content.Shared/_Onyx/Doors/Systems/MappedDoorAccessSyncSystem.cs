// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Doors.Systems;

/// <summary>
/// Copies mapper-assigned <see cref="AccessReaderComponent"/> access lists from a door
/// into the door electronics board contained in it.
/// Doors use <c>containerAccessProvider: board</c>, so their own lists are ignored
/// at runtime and mapping access directly on the door had no effect.
/// Sync happens when the board lands in the door container, which covers
/// <c>ContainerFill</c> spawning at map load.
/// </summary>
public sealed partial class MappedDoorAccessSyncSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AccessReaderComponent, EntInsertedIntoContainerMessage>(OnBoardInserted);
    }

    private void OnBoardInserted(Entity<AccessReaderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (ent.Comp.ContainerAccessProvider != args.Container.ID)
            return;

        if (ent.Comp.AccessLists.Count == 0 && ent.Comp.DenyTags.Count == 0)
            return;

        if (!TryComp<AccessReaderComponent>(args.Entity, out var boardReader))
            return;

        var board = (args.Entity, boardReader);

        if (ent.Comp.AccessLists.Count != 0)
        {
            var mapped = ent.Comp.AccessLists
                .Select(list => new HashSet<ProtoId<AccessLevelPrototype>>(list))
                .ToList();
            _access.TrySetAccesses(board, mapped);
        }

        if (ent.Comp.DenyTags.Count != 0)
            _access.SetDenyTags(board, new(ent.Comp.DenyTags));
    }
}
