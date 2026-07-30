// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Onyx.Religion;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;

namespace Content.Server._Onyx.Religion;

public sealed partial class WeakToHolySystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly DamageModifierSet BlockHoly = new()
    {
        Coefficients = new Dictionary<string, float> { ["Holy"] = 0f },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<DamageableComponent> ent, ref DamageModifyEvent args)
    {
        var takesHoly = TryComp<WeakToHolyComponent>(ent, out var weak) &&
            (weak.AlwaysTakeHoly || _inventory
                .GetHandOrInventoryEntities(ent.Owner, SlotFlags.WITHOUT_POCKET)
                .Any(HasComp<UnholyItemComponent>));

        if (!takesHoly)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, BlockHoly);
    }
}
