// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 yglop <95057024+yglop@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Stack;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;

namespace Content.Server._Onyx.MaterialEnergy;

public sealed partial class MaterialEnergySystem : EntitySystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MaterialEnergyComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<MaterialEnergyComponent> ent, ref InteractUsingEvent args)
    {
        if (ent.Comp.MaterialWhiteList == null ||
            !TryComp<PhysicalCompositionComponent>(args.Used, out var composition) ||
            !TryComp<StackComponent>(args.Used, out var stack) ||
            !TryComp<BatteryComponent>(ent, out var battery))
        {
            return;
        }

        var materialPerSheet = 0;
        foreach (var material in ent.Comp.MaterialWhiteList)
        {
            if (composition.MaterialComposition.TryGetValue(material.Id, out var quantity) && quantity > 0)
                materialPerSheet += quantity;
        }

        if (materialPerSheet <= 0)
            return;

        var freeCharge = battery.MaxCharge - _battery.GetCharge((ent.Owner, battery));
        var sheets = Math.Min(stack.Count, (int) MathF.Floor(freeCharge / materialPerSheet));
        if (sheets <= 0)
            return;

        var charge = sheets * materialPerSheet;
        var added = _battery.ChangeCharge((ent.Owner, battery), charge);
        if (added != charge)
        {
            _battery.ChangeCharge((ent.Owner, battery), -added);
            return;
        }

        if (!_stack.TryUse((args.Used, stack), sheets))
        {
            _battery.ChangeCharge((ent.Owner, battery), -charge);
            return;
        }

        args.Handled = true;
    }
}
