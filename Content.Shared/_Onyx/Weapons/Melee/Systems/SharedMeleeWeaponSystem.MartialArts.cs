using Content.Shared._Onyx.MartialArts;
using Robust.Shared.GameObjects;

namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    private void RaiseSelfDisarmCombo(EntityUid user, EntityUid meleeUid)
    {
        RaiseLocalEvent(user,
            new ComboAttackPerformedEvent(user, user, meleeUid, ComboAttackType.Disarm));
    }
}
