// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Bible.Components;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Content.Shared.NullRod.Components;

namespace Content.Server.NullRod;

public sealed partial class NullRodSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodComponent, GotEquippedEvent>(OnDidEquip);
        SubscribeLocalEvent<NullRodComponent, GotEquippedHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<NullRodComponent, GotUnequippedEvent>(OnDidUnequip);
        SubscribeLocalEvent<NullRodComponent, GotUnequippedHandEvent>(OnHandUnequipped);
    }

    private void OnDidEquip(Entity<NullRodComponent> ent, ref GotEquippedEvent args)
    {
        if (!HasComp<BibleUserComponent>(args.EquipTarget) || HasComp<NullRodOwnerComponent>(args.EquipTarget))
            return;

        EnsureComp<NullRodOwnerComponent>(args.EquipTarget);
    }

    private void OnHandEquipped(Entity<NullRodComponent> ent, ref GotEquippedHandEvent args)
    {
        if (!HasComp<BibleUserComponent>(args.User) || HasComp<NullRodOwnerComponent>(args.User))
            return;

        EnsureComp<NullRodOwnerComponent>(args.User);
    }

    private void OnDidUnequip(Entity<NullRodComponent> ent, ref GotUnequippedEvent args)
    {
        if (!HasComp<NullRodOwnerComponent>(args.EquipTarget))
            return;

        RemComp<NullRodOwnerComponent>(args.EquipTarget);
    }

    private void OnHandUnequipped(Entity<NullRodComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!HasComp<NullRodOwnerComponent>(args.User))
            return;

        RemComp<NullRodOwnerComponent>(args.User);
    }
}
