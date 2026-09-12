// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Construction;
using Content.Shared.Construction.Steps;
using Content.Shared.Examine;
using Content.Shared.Stacks;

namespace Content.Shared._Onyx.Construction;

[DataDefinition]
public sealed partial class TieredMachinePartConstructionGraphStep : EntityInsertConstructionGraphStep
{
    [DataField(required: true)]
    public MachinePartKind MachinePart;

    [DataField]
    public int Amount = 1;

    public override bool EntityValid(EntityUid uid, IEntityManager entityManager, IComponentFactory compFactory)
    {
        return entityManager.TryGetComponent(uid, out TieredMachinePartComponent? part)
            && part.Kind == MachinePart
            && (!entityManager.TryGetComponent(uid, out StackComponent? stack) || stack.Count >= Amount);
    }

    public override void DoExamine(ExaminedEvent examinedEvent)
    {
        examinedEvent.PushMarkup(Loc.GetString("construction-insert-material-entity",
            ("amount", Amount),
            ("materialName", Loc.GetString($"tiered-machine-part-kind-{MachinePart.ToString().ToLowerInvariant()}"))));
    }

    public override ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = "construction-presenter-material-step",
            Arguments = [("amount", Amount), ("material", Loc.GetString($"tiered-machine-part-kind-{MachinePart.ToString().ToLowerInvariant()}"))],
        };
    }
}
