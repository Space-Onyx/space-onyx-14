// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Containers;

namespace Content.Shared._Onyx.Abductor;

public sealed partial class SharedAbductorExperimentatorSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbductorExperimentatorComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<AbductorExperimentatorComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
    }

    private void OnContainerChanged(Entity<AbductorExperimentatorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        _appearance.SetData(ent, AbductorExperimentatorVisuals.Full, args.Container.ContainedEntities.Count > 0);
    }

    private void OnContainerChanged(Entity<AbductorExperimentatorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        _appearance.SetData(ent, AbductorExperimentatorVisuals.Full, args.Container.ContainedEntities.Count > 0);
    }
}
